namespace ConversaCore.Runtime;

/// <summary>
/// Carries one adaptive-card submission from a host/UI into
/// <see cref="IConversationRuntime.SubmitCardAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>WP2-scoped command placeholder (CC-200).</b> CC-300 now defines typed
/// <see cref="AdaptiveCardOutput"/> and <see cref="CardStateOutput"/> contracts. This type
/// intentionally carries only the minimum shape needed to
/// compile and use <see cref="IConversationRuntime"/> today: a card identifier plus the
/// submitted field values. CC-304 may refine or replace
/// this shape — for example, by validating <see cref="CardId"/> against the currently
/// dispatched card, by typing <see cref="Data"/> against a per-card schema, or by folding
/// submission into a broader correlated-interaction contract. Treat this as a stopgap,
/// not a final design.
/// </para>
/// <para>
/// The shape mirrors what the legacy runtime already threads through
/// <c>DomainAgentService.HandleCardSubmitAsync(Dictionary&lt;string, object&gt;, CancellationToken)</c>
/// and <c>AdaptiveCardInputCollectedEventArgs.Data</c> (see
/// <c>ConversaCore/Agentic/DomainAgentService.cs</c> and
/// <c>ConversaCore/Events/EventArgs/AdaptiveCardInputCollectedEventArgs.cs</c>): a bag of
/// submitted field values. The legacy path infers which card/activity the submission
/// belongs to from internal runtime state (<c>_activeTopic</c>'s current activity) rather
/// than from an explicit identifier supplied by the caller. <see cref="CardId"/> is added
/// here because an explicit, awaitable command surface should not rely on hidden mutable
/// state to know what a caller is responding to; it also gives a future implementation a
/// concrete value to validate against the last-dispatched card.
/// </para>
/// </remarks>
public sealed class CardSubmission
{
    /// <summary>
    /// The identifier of the adaptive card this submission answers. Must not be null,
    /// empty, or whitespace.
    /// </summary>
    public string CardId { get; }

    /// <summary>
    /// The submitted field values, keyed by input ID. Never null; defaults to an empty,
    /// read-only dictionary when no data is supplied (for example, a card whose only
    /// input is a single action button).
    /// </summary>
    public IReadOnlyDictionary<string, object> Data { get; }

    /// <summary>
    /// Creates a new card submission.
    /// </summary>
    /// <param name="cardId">The identifier of the card being answered. Must not be null, empty, or whitespace.</param>
    /// <param name="data">
    /// The submitted field values, keyed by input ID. When null, defaults to an empty
    /// dictionary.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="cardId"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public CardSubmission(string cardId, IReadOnlyDictionary<string, object>? data = null)
    {
        if (string.IsNullOrWhiteSpace(cardId))
            throw new ArgumentException("Card ID must not be null, empty, or whitespace.", nameof(cardId));

        CardId = cardId;
        Data = data ?? new Dictionary<string, object>();
    }
}
