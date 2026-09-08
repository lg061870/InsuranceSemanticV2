namespace ConversaCore.Runtime;

/// <summary>
/// An asynchronous, disposable subscription to one conversation's output stream, obtained
/// from <see cref="IConversationRuntime.Subscribe"/>.
/// </summary>
/// <remarks>
/// <para>
/// CC-300 replaces the WP2 object placeholder with the domain-neutral
/// <see cref="ConversationOutput"/> hierarchy. Buffering, ordering, multi-subscriber fan-out,
/// and subscriber-failure isolation remain implementation concerns for CC-301.
/// </para>
/// <para>
/// The shape follows ADR-003 ("the runtime emits typed <c>ConversationOutput</c> values
/// through asynchronous disposable subscriptions, ordered per conversation ... UI receives
/// immutable payloads or snapshots") and target architecture section 6's rule that "output
/// is delivered through an asynchronous, disposable subscription rather than
/// <c>async void</c> event chains." A pull-based <see cref="IAsyncEnumerable{T}"/> is used
/// instead of a .NET event so a consumer awaits and enumerates output on its own schedule,
/// with backpressure, cancellation, and disposal expressed through ordinary async
/// enumeration and <see cref="IAsyncDisposable"/> rather than through subscribe/unsubscribe
/// event-handler bookkeeping. Concrete buffering, ordering, multi-subscriber fan-out, and
/// subscriber-failure isolation behavior are implementation concerns reserved for CC-301,
/// not decided here.
/// </para>
/// <para>
/// Disposing the subscription (via <see cref="IAsyncDisposable.DisposeAsync"/>) must end
/// the underlying enumeration predictably and release any resources the runtime allocated
/// for this subscriber, without affecting other subscribers or the conversation itself.
/// </para>
/// </remarks>
public interface IConversationOutputSubscription : IAsyncDisposable
{
    /// <summary>
    /// Streams this conversation's output items in delivery order, from the point this
    /// subscription was created, until the subscription is disposed or
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    /// <param name="cancellationToken">A token that ends enumeration when cancelled.</param>
    /// <returns>An asynchronous stream of output items.</returns>
    IAsyncEnumerable<ConversationOutput> ReadAllAsync(CancellationToken cancellationToken = default);
}
