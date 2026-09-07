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
    private readonly IWorkflowOutputDispatcher _outputDispatcher;
    private readonly IServiceProvider _services;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private ITopic? _activeExecution;
    private bool _disposed;

    /// <summary>Creates a runner for exactly one scoped conversation.</summary>
    public WorkflowRunner(IConversationSession session, ITopicActivator activator, IServiceProvider services,
        IWorkflowOutputDispatcher? outputDispatcher = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _activator = activator ?? throw new ArgumentNullException(nameof(activator));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _outputDispatcher = outputDispatcher ?? NullWorkflowOutputDispatcher.Instance;
    }

    /// <inheritdoc />
    public bool HasActiveExecution => _activeExecution is not null;

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
            return await ExecuteActiveAsync(_session.ActiveTopic, _activeExecution, message, cancellationToken).ConfigureAwait(false);
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
        string message, CancellationToken cancellationToken)
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

        if (state is WorkflowExecutionState.Completed or WorkflowExecutionState.NotHandled)
        {
            _activeExecution = null;
            _session.SetActiveTopic(null);
        }

        await _outputDispatcher.DispatchAsync(outcome, cancellationToken).ConfigureAwait(false);
        return outcome;
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
}
