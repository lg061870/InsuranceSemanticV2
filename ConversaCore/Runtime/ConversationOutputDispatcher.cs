using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ConversaCore.Runtime;

/// <summary>Scoped ordered fan-out dispatcher for one conversation.</summary>
/// <remarks>Publication never invokes subscriber code. Each subscription has an independent
/// unbounded channel, so a failed, cancelled, or disposed consumer cannot interrupt execution or
/// another subscriber. The containing scope must dispose this dispatcher.</remarks>
public sealed class ConversationOutputDispatcher : IConversationOutputDispatcher
{
    private readonly object _sync = new();
    private readonly string _conversationId;
    private readonly Dictionary<long, Channel<ConversationOutput>> _subscribers = [];
    private long _nextSubscriberId;
    private bool _disposed;

    /// <summary>Creates a dispatcher bound to the supplied conversation session.</summary>
    public ConversationOutputDispatcher(IConversationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _conversationId = session.ConversationId;
    }

    /// <inheritdoc />
    public IConversationOutputSubscription Subscribe()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var id = ++_nextSubscriberId;
            var channel = Channel.CreateUnbounded<ConversationOutput>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = false,
                SingleReader = true,
                SingleWriter = false
            });
            _subscribers.Add(id, channel);
            return new Subscription(id, channel.Reader, RemoveSubscription);
        }
    }

    /// <inheritdoc />
    public Task DispatchAsync(ConversationOutput output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(output.ConversationId, _conversationId, StringComparison.Ordinal))
            throw new ArgumentException(
                $"Output conversation '{output.ConversationId}' does not match dispatcher conversation '{_conversationId}'.",
                nameof(output));

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            foreach (var channel in _subscribers.Values)
            {
                if (!channel.Writer.TryWrite(output))
                    throw new InvalidOperationException("A current conversation output subscriber rejected publication.");
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Channel<ConversationOutput>[] channels;
        lock (_sync)
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            channels = _subscribers.Values.ToArray();
            _subscribers.Clear();
        }

        foreach (var channel in channels) channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private void RemoveSubscription(long id)
    {
        Channel<ConversationOutput>? channel;
        lock (_sync)
        {
            if (!_subscribers.Remove(id, out channel)) return;
        }
        channel.Writer.TryComplete();
    }

    private sealed class Subscription(
        long id,
        ChannelReader<ConversationOutput> reader,
        Action<long> remove) : IConversationOutputSubscription
    {
        private int _enumerated;
        private int _disposed;

        public IAsyncEnumerable<ConversationOutput> ReadAllAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _enumerated, 1) != 0)
                throw new InvalidOperationException("A conversation output subscription can be enumerated only once.");
            return ReadAndReleaseAsync(cancellationToken);
        }

        private async IAsyncEnumerable<ConversationOutput> ReadAndReleaseAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var output in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                    yield return output;
            }
            finally
            {
                await DisposeAsync().ConfigureAwait(false);
            }
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) remove(id);
            return ValueTask.CompletedTask;
        }
    }
}
