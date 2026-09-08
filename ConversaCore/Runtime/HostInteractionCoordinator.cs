using System.Collections.Concurrent;

namespace ConversaCore.Runtime;

/// <summary>Scoped implementation of correlated host-interaction request/response semantics.</summary>
public sealed class HostInteractionCoordinator : IHostInteractionCoordinator
{
    private readonly IConversationSession _session;
    private readonly IConversationOutputDispatcher _dispatcher;
    private readonly ConcurrentDictionary<string, IPendingInteraction> _pending = new(StringComparer.Ordinal);
    private readonly object _lifecycleSync = new();
    private int _disposed;

    /// <summary>Creates a coordinator for one conversation scope.</summary>
    public HostInteractionCoordinator(IConversationSession session, IConversationOutputDispatcher dispatcher)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <inheritdoc />
    public async Task<TResponse> RequestAsync<TRequest, TResponse>(string interactionName, int version,
        TRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
        where TRequest : notnull
        where TResponse : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requestId = Guid.NewGuid().ToString("N");
        var pending = new PendingInteraction<TResponse>();
        lock (_lifecycleSync)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            if (!_pending.TryAdd(requestId, pending))
                throw new InvalidOperationException("Unable to allocate a unique host-interaction correlation identifier.");
            try { _session.RegisterPendingHostInteraction(requestId); }
            catch
            {
                _pending.TryRemove(requestId, out _);
                throw;
            }
        }

        try
        {
            var output = new HostInteractionRequest<TRequest, TResponse>(
                _session.ConversationId, requestId, interactionName, version, timeout, request);
            await _dispatcher.DispatchAsync(output, cancellationToken).ConfigureAwait(false);
            try
            {
                return await pending.Completion.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                throw new HostInteractionTimeoutException(requestId, timeout, ex);
            }
        }
        finally
        {
            if (_pending.TryRemove(requestId, out _))
                _session.TryResolvePendingHostInteraction(requestId);
        }
    }

    /// <inheritdoc />
    public Task RespondAsync(HostInteractionResponse response, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_pending.TryGetValue(response.RequestId, out var pending))
            throw new HostInteractionNotPendingException(response.RequestId);

        object value;
        try
        {
            value = pending.ReadResponse(response);
        }
        catch (Exception ex)
        {
            throw new HostInteractionResponseTypeException(response.RequestId, pending.ResponseType, ex);
        }

        if (!_pending.TryRemove(response.RequestId, out var removed) || !ReferenceEquals(removed, pending))
            throw new HostInteractionNotPendingException(response.RequestId);
        _session.TryResolvePendingHostInteraction(response.RequestId);
        pending.Complete(value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        List<(string RequestId, IPendingInteraction Pending)> cancelled = [];
        lock (_lifecycleSync)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
            foreach (var pair in _pending.ToArray())
            {
                if (!_pending.TryRemove(pair.Key, out var pending)) continue;
                _session.TryResolvePendingHostInteraction(pair.Key);
                cancelled.Add((pair.Key, pending));
            }
        }
        foreach (var item in cancelled)
            item.Pending.Cancel(new ObjectDisposedException(nameof(HostInteractionCoordinator)));
        return ValueTask.CompletedTask;
    }

    private interface IPendingInteraction
    {
        Type ResponseType { get; }
        object ReadResponse(HostInteractionResponse response);
        void Complete(object value);
        void Cancel(Exception exception);
    }

    private sealed class PendingInteraction<TResponse> : IPendingInteraction where TResponse : notnull
    {
        private readonly TaskCompletionSource<TResponse> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Type ResponseType => typeof(TResponse);
        public Task<TResponse> Completion => _completion.Task;
        public object ReadResponse(HostInteractionResponse response) => response.ReadPayload<TResponse>();
        public void Complete(object value) => _completion.TrySetResult((TResponse)value);
        public void Cancel(Exception exception) => _completion.TrySetException(exception);
    }
}
