using ConversaCore.Models;
using ConversaCore.Registration;
using ConversaCore.Topics;

namespace ConversaCore.Runtime;

/// <summary>Scoped implementation of <see cref="IWorkflowRunner"/>.</summary>
/// <remarks>All commands are serialized by this instance's gate. The only retained mutable object
/// is the live <see cref="ITopic"/> created for the current conversation; descriptor metadata stays
/// in <see cref="IConversationSession"/>. It invokes <see cref="ITopic.ProcessMessageAsync"/> as the
/// isolated compatibility seam for existing topics, rather than wiring activity events itself.</remarks>
public sealed class WorkflowRunner : IWorkflowRunner, IDisposable
{
    private readonly IConversationSession _session;
    private readonly ITopicActivator _activator;
    private readonly ITopicCatalog _catalog;
    private readonly IWorkflowOutputDispatcher _outputDispatcher;
    private readonly IServiceProvider _services;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly Stack<SuspendedExecution> _suspendedParents = new();
    private ITopic? _activeExecution;
    private bool _disposed;

    /// <summary>Creates a runner for exactly one scoped conversation.</summary>
    public WorkflowRunner(IConversationSession session, ITopicActivator activator, ITopicCatalog catalog, IServiceProvider services,
        IWorkflowOutputDispatcher? outputDispatcher = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _activator = activator ?? throw new ArgumentNullException(nameof(activator));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _outputDispatcher = outputDispatcher ?? NullWorkflowOutputDispatcher.Instance;
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
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_activeExecution is null || _session.ActiveTopic is null)
                throw new InvalidOperationException("No active workflow execution is available to receive input.");
            return await ExecuteActiveAsync(_session.ActiveTopic, _activeExecution, message, cancellationToken,
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
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_activeExecution is null || _session.ActiveTopic is null)
                throw new InvalidOperationException("No active workflow execution is available to interrupt.");
            _suspendedParents.Push(new SuspendedExecution(_session.ActiveTopic, _activeExecution,
                RunnerOwnsSessionCall: false, SuspensionKind.Interruption));
            _activeExecution = await _activator.ActivateAsync(topic.TopicId, _services, cancellationToken).ConfigureAwait(false);
            _session.SetActiveTopic(topic);
            return await ExecuteActiveAsync(topic, _activeExecution, message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<WorkflowExecutionOutcome> ExecuteNewAsync(TopicDescriptor topic, string message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ThrowIfDisposed();
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_suspendedParents.Count != 0)
                throw new InvalidOperationException("Cannot replace a workflow while a parent is awaiting subtopic completion.");
            // A new activation supersedes an old, nonterminal execution only when the caller has
            // already made that routing/interrupt decision. CC-207 supplies that policy.
            _activeExecution = await _activator.ActivateAsync(topic.TopicId, _services, cancellationToken).ConfigureAwait(false);
            _session.SetActiveTopic(topic);
            return await ExecuteActiveAsync(topic, _activeExecution, message, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
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

        if (state == WorkflowExecutionState.Completed || (state == WorkflowExecutionState.NotHandled && !retainWhenUnhandled))
        {
            _activeExecution = null;
            _session.SetActiveTopic(null);
        }

        await _outputDispatcher.DispatchAsync(outcome, cancellationToken).ConfigureAwait(false);
        if (state == WorkflowExecutionState.Completed || (state == WorkflowExecutionState.NotHandled && !retainWhenUnhandled))
            return await ResumeParentIfNeededAsync(outcome, cancellationToken).ConfigureAwait(false);
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

        _suspendedParents.Push(new SuspendedExecution(parentDescriptor, parentExecution, runnerOwnsSessionCall,
            SuspensionKind.Subtopic));
        _activeExecution = await _activator.ActivateAsync(childDescriptor.TopicId, _services, cancellationToken).ConfigureAwait(false);
        _session.SetActiveTopic(childDescriptor);
        return await ExecuteActiveAsync(childDescriptor, _activeExecution, string.Empty, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkflowExecutionOutcome> ResumeParentIfNeededAsync(WorkflowExecutionOutcome childOutcome,
        CancellationToken cancellationToken)
    {
        if (_suspendedParents.Count == 0)
            return childOutcome;

        var parent = _suspendedParents.Pop();
        if (parent.RunnerOwnsSessionCall)
            _session.PopTopicCall(childOutcome);

        _activeExecution = parent.Execution;
        _session.SetActiveTopic(parent.Descriptor);
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
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _activeExecution = null;
        _suspendedParents.Clear();
        _operationGate.Dispose();
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
        bool RunnerOwnsSessionCall, SuspensionKind Kind);
}
