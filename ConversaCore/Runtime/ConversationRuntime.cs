using ConversaCore.Registration;

namespace ConversaCore.Runtime;

/// <summary>
/// Framework-owned, scoped facade that composes routing, workflow execution, output, and
/// host-interaction services for one conversation.
/// </summary>
public sealed class ConversationRuntime : IConversationRuntime, IAsyncDisposable
{
    private readonly object _lifecycleGate = new();
    private readonly IWorkflowRunner _runner;
    private readonly IConversationMessageCoordinator _messages;
    private readonly IConversationOutputDispatcher _outputs;
    private readonly IHostInteractionCoordinator _hostInteractions;
    private readonly TopicDescriptor _startTopic;
    private Task? _startTask;
    private Task? _resetTask;
    private Task? _disposeTask;

    /// <summary>Creates one facade over services from the same conversation scope.</summary>
    public ConversationRuntime(
        IConversationSession session,
        ITopicCatalog topics,
        IWorkflowRunner runner,
        IConversationMessageCoordinator messages,
        IConversationOutputDispatcher outputs,
        IHostInteractionCoordinator hostInteractions,
        ConversationRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(topics);
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
        _hostInteractions = hostInteractions ?? throw new ArgumentNullException(nameof(hostInteractions));
        ArgumentNullException.ThrowIfNull(options);

        ConversationId = session.ConversationId;
        if (!topics.TryGetDescriptor(options.StartTopicId, out var startTopic) || startTopic is null)
            throw new InvalidOperationException(
                $"Conversation start topic '{options.StartTopicId}' is not registered.");
        _startTopic = startTopic;
    }

    /// <inheritdoc />
    public string ConversationId { get; }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            return _startTask ??= _runner.StartAsync(_startTopic, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        await AwaitStartedAsync(cancellationToken).ConfigureAwait(false);
        await _messages.ProcessAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SubmitCardAsync(CardSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        await AwaitStartedAsync(cancellationToken).ConfigureAwait(false);
        await _runner.SubmitCardAsync(submission, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task RespondToHostInteractionAsync(
        HostInteractionResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        lock (_lifecycleGate) ThrowIfDisposed();
        return _hostInteractions.RespondAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            if (_resetTask is { IsCompleted: false }) return _resetTask;
            _resetTask = ResetCoreAsync(cancellationToken);
            _startTask = _resetTask;
            return _resetTask;
        }
    }

    /// <inheritdoc />
    public IConversationOutputSubscription Subscribe()
    {
        lock (_lifecycleGate) ThrowIfDisposed();
        return _outputs.Subscribe();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_lifecycleGate)
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
    }

    private async Task ResetCoreAsync(CancellationToken cancellationToken)
    {
        await _runner.ResetAsync(cancellationToken).ConfigureAwait(false);
        await _runner.StartAsync(_startTopic, cancellationToken).ConfigureAwait(false);
    }

    private async Task AwaitStartedAsync(CancellationToken cancellationToken)
    {
        Task startTask;
        lock (_lifecycleGate)
        {
            ThrowIfDisposed();
            startTask = _startTask ?? throw new InvalidOperationException(
                "The conversation has not been started.");
        }
        await startTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DisposeCoreAsync()
    {
        Exception? failure = null;
        try
        {
            if (_runner is IAsyncDisposable asyncRunner)
                await asyncRunner.DisposeAsync().ConfigureAwait(false);
            else if (_runner is IDisposable runner)
                runner.Dispose();
            else
                await _runner.CancelAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            await _hostInteractions.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = failure is null ? exception : new AggregateException(failure, exception);
        }

        try
        {
            await _outputs.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = failure is null ? exception : new AggregateException(failure, exception);
        }

        if (failure is not null) throw failure;
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposeTask is not null, this);
}
