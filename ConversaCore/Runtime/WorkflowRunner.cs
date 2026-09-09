using ConversaCore.Models;
using ConversaCore.Registration;
using ConversaCore.Events;
using ConversaCore.Core;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Flow = ConversaCore.TopicFlow.TopicFlow;

namespace ConversaCore.Runtime;

/// <summary>Scoped implementation of <see cref="IWorkflowRunner"/>.</summary>
/// <remarks>All commands are serialized by this instance's gate. The only retained mutable object
/// is the live <see cref="ITopic"/> created for the current conversation; descriptor metadata stays
/// in <see cref="IConversationSession"/>. It invokes <see cref="ITopic.ProcessMessageAsync"/> as the
/// isolated compatibility seam for existing topics, rather than wiring activity events itself.</remarks>
public sealed class WorkflowRunner : IWorkflowRunner, IDisposable, IAsyncDisposable
{
    private readonly IConversationSession _session;
    private readonly ITopicActivator _activator;
    private readonly ITopicCatalog _catalog;
    private readonly IWorkflowOutputDispatcher _outputDispatcher;
    private readonly ILegacyTopicOutputAdapter? _legacyOutputAdapter;
    private readonly IServiceProvider _services;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _cancellationSync = new();
    private readonly Stack<SuspendedExecution> _suspendedParents = new();
    private CancellationTokenSource _executionCancellation = new();
    private ITopic? _activeExecution;
    private IAsyncDisposable? _activeOutputLease;
    private bool _disposed;

    /// <summary>Creates a runner for exactly one scoped conversation.</summary>
    public WorkflowRunner(IConversationSession session, ITopicActivator activator, ITopicCatalog catalog, IServiceProvider services,
        IWorkflowOutputDispatcher? outputDispatcher = null,
        ILegacyTopicOutputAdapter? legacyOutputAdapter = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _activator = activator ?? throw new ArgumentNullException(nameof(activator));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _outputDispatcher = outputDispatcher ?? NullWorkflowOutputDispatcher.Instance;
        _legacyOutputAdapter = legacyOutputAdapter;
    }

    /// <inheritdoc />
    public bool HasActiveExecution => _activeExecution is not null;

    /// <inheritdoc />
    public int PendingSubtopicDepth => _suspendedParents.Count;

    /// <inheritdoc />
    public Task<WorkflowExecutionOutcome> StartAsync(TopicDescriptor topic, CancellationToken cancellationToken = default)
        => ExecuteNewAsync(topic, string.Empty, cancellationToken);

    /// <inheritdoc />
    public Task<WorkflowExecutionOutcome> ActivateAndDeliverAsync(TopicDescriptor topic, string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return ExecuteNewAsync(topic, message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowExecutionOutcome> DeliverToActiveAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ThrowIfDisposed();
        using var operation = CreateOperationCancellation(cancellationToken);
        await _operationGate.WaitAsync(operation.Token).ConfigureAwait(false);
        try
        {
            if (_activeExecution is null || _session.ActiveTopic is null)
                throw new InvalidOperationException("No active workflow execution is available to receive input.");
            return await ExecuteActiveAsync(_session.ActiveTopic, _activeExecution, message, operation.Token,
                retainWhenUnhandled: true).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<WorkflowExecutionOutcome> InterruptAndDeliverAsync(TopicDescriptor topic, string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ThrowIfDisposed();
        using var operation = CreateOperationCancellation(cancellationToken);
        await _operationGate.WaitAsync(operation.Token).ConfigureAwait(false);
        try
        {
            if (_activeExecution is null || _session.ActiveTopic is null)
                throw new InvalidOperationException("No active workflow execution is available to interrupt.");
            var parent = new SuspendedExecution(_session.ActiveTopic, _activeExecution,
                _activeOutputLease, RunnerOwnsSessionCall: false, SuspensionKind.Interruption);
            _suspendedParents.Push(parent);
            _activeExecution = null;
            _activeOutputLease = null;
            try
            {
                await ActivateAsync(topic, operation.Token).ConfigureAwait(false);
            }
            catch
            {
                _suspendedParents.Pop();
                Restore(parent);
                throw;
            }
            return await ExecuteActiveAsync(topic, _activeExecution ?? throw new InvalidOperationException(
                "Topic activation completed without an active execution."), message, operation.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<WorkflowExecutionOutcome> SubmitCardAsync(
        CardSubmission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ThrowIfDisposed();
        using var operation = CreateOperationCancellation(cancellationToken);
        await _operationGate.WaitAsync(operation.Token).ConfigureAwait(false);
        try
        {
            if (_activeExecution is not Flow flow || _session.ActiveTopic is null)
                throw new InvalidOperationException("No active TopicFlow is available to receive a card submission.");

            var activity = flow.GetCurrentActivity();
            if (activity is not IAdaptiveCardActivity cardActivity)
                throw new InvalidOperationException("The active workflow activity does not accept adaptive-card input.");
            if (!string.Equals(activity.Id, submission.CardId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Card '{submission.CardId}' is not the active card '{activity.Id}'.");

            operation.Token.ThrowIfCancellationRequested();
            cardActivity.OnInputCollected(new AdaptiveCardInputCollectedEventArgs(
                submission.Data.ToDictionary(pair => pair.Key, pair => pair.Value)));

            if (activity.CurrentState == ActivityState.WaitingForUserInput)
                return new WorkflowExecutionOutcome(
                    _session.ActiveTopic,
                    WorkflowExecutionState.WaitingForInput,
                    null,
                    null,
                    true,
                    null);

            return await ExecuteActiveAsync(
                _session.ActiveTopic,
                _activeExecution ?? throw new InvalidOperationException(
                    "Card submission completed without an active execution."),
                string.Empty,
                operation.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken = default) => ClearAfterCancellationAsync(resetSession: false, cancellationToken);

    /// <inheritdoc />
    public Task ResetAsync(CancellationToken cancellationToken = default) => ClearAfterCancellationAsync(resetSession: true, cancellationToken);

    private async Task<WorkflowExecutionOutcome> ExecuteNewAsync(TopicDescriptor topic, string message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ThrowIfDisposed();
        using var operation = CreateOperationCancellation(cancellationToken);
        await _operationGate.WaitAsync(operation.Token).ConfigureAwait(false);
        try
        {
            if (_suspendedParents.Count != 0)
                throw new InvalidOperationException("Cannot replace a workflow while a parent is awaiting subtopic completion.");
            // A new activation supersedes an old, nonterminal execution only when the caller has
            // already made that routing/interrupt decision. CC-207 supplies that policy.
            await ReleaseActiveExecutionAsync().ConfigureAwait(false);
            await ActivateAsync(topic, operation.Token).ConfigureAwait(false);
            return await ExecuteActiveAsync(topic, _activeExecution ?? throw new InvalidOperationException(
                "Topic activation completed without an active execution."), message, operation.Token).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task ClearAfterCancellationAsync(bool resetSession, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        lock (_cancellationSync) _executionCancellation.Cancel();
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Exception? failure = null;
        try
        {
            failure = await ReleaseRetainedExecutionsAsync().ConfigureAwait(false);
            _suspendedParents.Clear();
            if (resetSession) _session.Reset(); else _session.SetActiveTopic(null);
            lock (_cancellationSync)
            {
                _executionCancellation.Dispose();
                _executionCancellation = new CancellationTokenSource();
            }
        }
        catch (Exception exception)
        {
            failure = Combine(failure, exception);
        }
        finally { _operationGate.Release(); }
        if (failure is not null) throw failure;
    }

    private CancellationTokenSource CreateOperationCancellation(CancellationToken callerToken)
    {
        lock (_cancellationSync)
            return CancellationTokenSource.CreateLinkedTokenSource(callerToken, _executionCancellation.Token);
    }

    private async Task<WorkflowExecutionOutcome> ExecuteActiveAsync(TopicDescriptor descriptor, ITopic execution,
        string message, CancellationToken cancellationToken, bool retainWhenUnhandled = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Legacy topic implementations expose this single awaitable compatibility entry point.
        // The runner never subscribes to their activity events or reads mutable workflow context.
        var result = await execution.ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);
        if (result is null)
            throw new InvalidOperationException($"Topic '{descriptor.TopicId}' returned a null TopicResult.");

        var state = GetState(result);
        var outcome = new WorkflowExecutionOutcome(descriptor, state, result.Response, result.AdaptiveCardJson,
            result.IsHandled, result.IsWaitingForSubTopic ? result.NextTopicName : null);

        // The parent wait is observable before its child starts. The runner, not an activity
        // subscription, then performs the hand-down as one serialized continuation.
        if (state == WorkflowExecutionState.WaitingForSubtopic)
        {
            await _outputDispatcher.DispatchAsync(outcome, cancellationToken).ConfigureAwait(false);
            return await StartRequestedSubtopicAsync(descriptor, execution, result.NextTopicName!, cancellationToken)
                .ConfigureAwait(false);
        }

        var releasesActivation = state == WorkflowExecutionState.Completed ||
                                 (state == WorkflowExecutionState.NotHandled && !retainWhenUnhandled);
        if (releasesActivation) _session.SetActiveTopic(null);

        await _outputDispatcher.DispatchAsync(outcome, cancellationToken).ConfigureAwait(false);
        if (releasesActivation)
        {
            await ReleaseActiveExecutionAsync().ConfigureAwait(false);
            return await ResumeParentIfNeededAsync(outcome, cancellationToken).ConfigureAwait(false);
        }
        return outcome;
    }

    private async Task<WorkflowExecutionOutcome> StartRequestedSubtopicAsync(TopicDescriptor parentDescriptor,
        ITopic parentExecution, string subtopicId, CancellationToken cancellationToken)
    {
        if (!_catalog.TryGetDescriptor(subtopicId, out var childDescriptor) || childDescriptor is null)
            throw new TopicActivationException(subtopicId,
                $"Topic '{parentDescriptor.TopicId}' requested unregistered subtopic '{subtopicId}'.");
        if (_suspendedParents.Any(frame => frame.Descriptor.Equals(childDescriptor)) || parentDescriptor.Equals(childDescriptor))
            throw new InvalidOperationException($"Subtopic cycle detected for '{childDescriptor.TopicId}'.");

        // TriggerTopicActivity in the legacy compatibility path may have already pushed this
        // exact child. New runner-owned paths do not; only the latter is popped by this runner.
        var runnerOwnsSessionCall = !_session.IsTopicInCallStack(childDescriptor.TopicId);
        if (runnerOwnsSessionCall)
            _session.PushTopicCall(parentDescriptor.TopicId, childDescriptor.TopicId);

        var parent = new SuspendedExecution(parentDescriptor, parentExecution, _activeOutputLease, runnerOwnsSessionCall,
            SuspensionKind.Subtopic);
        _suspendedParents.Push(parent);
        _activeExecution = null;
        _activeOutputLease = null;
        try
        {
            await ActivateAsync(childDescriptor, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _suspendedParents.Pop();
            if (runnerOwnsSessionCall) _session.PopTopicCall();
            Restore(parent);
            throw;
        }
        return await ExecuteActiveAsync(childDescriptor, _activeExecution ?? throw new InvalidOperationException(
            "Subtopic activation completed without an active execution."), string.Empty, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkflowExecutionOutcome> ResumeParentIfNeededAsync(WorkflowExecutionOutcome childOutcome,
        CancellationToken cancellationToken)
    {
        if (_suspendedParents.Count == 0)
            return childOutcome;

        var parent = _suspendedParents.Pop();
        if (parent.RunnerOwnsSessionCall)
            _session.PopTopicCall(childOutcome);

        Restore(parent);
        // Existing ITopic exposes text input only. New typed child-result delivery is deliberately
        // deferred; legacy TopicFlow uses these conventional messages to advance its cursor.
        var resumeMessage = parent.Kind == SuspensionKind.Subtopic ? "Sub-topic completed" : "Interrupted topic completed";
        return await ExecuteActiveAsync(parent.Descriptor, parent.Execution, resumeMessage, cancellationToken)
            .ConfigureAwait(false);
    }

    private static WorkflowExecutionState GetState(TopicResult result) =>
        result.IsWaitingForSubTopic ? WorkflowExecutionState.WaitingForSubtopic :
        result.RequiresInput || result.KeepActive ? WorkflowExecutionState.WaitingForInput :
        result.IsCompleted ? WorkflowExecutionState.Completed :
        result.IsHandled ? WorkflowExecutionState.Completed : WorkflowExecutionState.NotHandled;

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(WorkflowRunner));
    }

    /// <inheritdoc />
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_cancellationSync)
        {
            if (_disposed) return;
            _disposed = true;
            _executionCancellation.Cancel();
        }

        await _operationGate.WaitAsync().ConfigureAwait(false);
        Exception? failure = null;
        try
        {
            failure = await ReleaseRetainedExecutionsAsync().ConfigureAwait(false);
            _session.SetActiveTopic(null);
        }
        catch (Exception exception)
        {
            failure = Combine(failure, exception);
        }
        finally
        {
            _executionCancellation.Dispose();
            _operationGate.Release();
            _operationGate.Dispose();
        }
        if (failure is not null) throw failure;
    }

    private async Task ActivateAsync(TopicDescriptor descriptor, CancellationToken cancellationToken)
    {
        var execution = await _activator
            .ActivateAsync(descriptor.TopicId, _services, cancellationToken)
            .ConfigureAwait(false);
        IAsyncDisposable? outputLease = null;
        try
        {
            outputLease = _legacyOutputAdapter?.Attach(descriptor, execution);
        }
        catch
        {
            await ReleaseExecutionAsync(execution, null).ConfigureAwait(false);
            throw;
        }

        _activeExecution = execution;
        _activeOutputLease = outputLease;
        _session.SetActiveTopic(descriptor);
    }

    private async Task ReleaseActiveExecutionAsync()
    {
        var execution = _activeExecution;
        var outputLease = _activeOutputLease;
        _activeExecution = null;
        _activeOutputLease = null;
        if (execution is not null)
            await ReleaseExecutionAsync(execution, outputLease).ConfigureAwait(false);
        else if (outputLease is not null)
            await outputLease.DisposeAsync().ConfigureAwait(false);
    }

    private async Task<Exception?> ReleaseRetainedExecutionsAsync()
    {
        Exception? failure = null;
        try
        {
            await ReleaseActiveExecutionAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = Combine(failure, exception);
        }

        while (_suspendedParents.Count > 0)
        {
            var parent = _suspendedParents.Pop();
            try
            {
                await ReleaseAsync(parent).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = Combine(failure, exception);
            }
        }

        return failure;
    }

    private static Exception Combine(Exception? current, Exception next) =>
        current is null ? next : new AggregateException(current, next);

    private static Task ReleaseAsync(SuspendedExecution execution) =>
        ReleaseExecutionAsync(execution.Execution, execution.OutputLease);

    private static async Task ReleaseExecutionAsync(ITopic execution, IAsyncDisposable? outputLease)
    {
        Exception? failure = null;
        try
        {
            if (outputLease is not null)
                await outputLease.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            if (execution is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (execution is ITerminable terminable)
                await terminable.TerminateAsync().ConfigureAwait(false);
            else if (execution is IDisposable disposable)
                disposable.Dispose();
        }
        catch (Exception exception)
        {
            failure = failure is null ? exception : new AggregateException(failure, exception);
        }

        if (failure is not null) throw failure;
    }

    private void Restore(SuspendedExecution execution)
    {
        _activeExecution = execution.Execution;
        _activeOutputLease = execution.OutputLease;
        _session.SetActiveTopic(execution.Descriptor);
    }

    private sealed class NullWorkflowOutputDispatcher : IWorkflowOutputDispatcher
    {
        public static NullWorkflowOutputDispatcher Instance { get; } = new();
        public Task DispatchAsync(WorkflowExecutionOutcome outcome, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private enum SuspensionKind { Subtopic, Interruption }

    private sealed record SuspendedExecution(TopicDescriptor Descriptor, ITopic Execution,
        IAsyncDisposable? OutputLease, bool RunnerOwnsSessionCall, SuspensionKind Kind);
}
