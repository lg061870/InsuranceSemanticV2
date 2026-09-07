namespace ConversaCore.Runtime;

/// <summary>
/// An asynchronous, disposable subscription to one conversation's output stream, obtained
/// from <see cref="IConversationRuntime.Subscribe"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>WP2-scoped placeholder (CC-200).</b> The typed <c>ConversationOutput</c> hierarchy
/// (messages, cards, card state, prompt state, topic/activity lifecycle, host
/// notifications, host interaction requests — see target architecture section 9.1) is
/// CC-300's job and does not exist yet. This subscription therefore streams <see
/// cref="object"/> items rather than a typed <c>ConversationOutput</c> base type. WP3 will
/// very likely replace the item type with <c>ConversationOutput</c> (or make this
/// interface generic over it) once that hierarchy is defined; no output-shape decisions
/// are made by this placeholder. Treat this as a stopgap, not a final design.
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
    IAsyncEnumerable<object> ReadAllAsync(CancellationToken cancellationToken = default);
}
