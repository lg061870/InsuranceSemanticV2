using ConversaCore.Context;
using ConversaCore.Registration;

namespace ConversaCore.Runtime;

/// <summary>
/// Owns the state that lives for the entire duration of one customer conversation:
/// conversation identity, the authenticated subject, the active topic, the topic call
/// stack, shared conversation-scoped values, and the pending-host-interaction registry.
/// This is CC-201's implementation of the "conversation session" scope defined by target
/// architecture section 8 ("Conversation and workflow state") and referenced by CC-200's
/// <see cref="IConversationRuntime"/> as the state a future concrete runtime composes
/// with an <c>ITopicRouter</c> and <c>IWorkflowRunner</c> (CC-204, CC-205 — not built by
/// this ticket).
/// </summary>
/// <remarks>
/// <para><b>Scope of this ticket (CC-201).</b></para>
/// <para>
/// This type and its implementation own session-level bookkeeping only. It does not
/// resolve or activate topics (<c>ITopicCatalog</c>/<c>ITopicActivator</c> — CC-202,
/// CC-203), does not execute activities or make routing decisions
/// (<c>IWorkflowRunner</c>/<c>ITopicRouter</c> — CC-204, CC-205), and is not itself a
/// concrete <see cref="IConversationRuntime"/> implementation. It also does not build the
/// host-interaction dispatch machinery, timeouts, or typed request/response correlation
/// that CC-304 owns (see ADR-004) — it only maintains the bookkeeping structure a future
/// dispatcher needs (see <see cref="RegisterPendingHostInteraction"/> remarks).
/// </para>
/// <para><b>Design decision: compose the existing <see cref="IConversationContext"/>, don't reimplement it.</b></para>
/// <para>
/// The existing <see cref="IConversationContext"/> (<c>ConversaCore.Context</c>, read but
/// not modified for this ticket) already implements a substantial, exercised slice of what
/// target architecture section 8's "conversation session" row asks for: conversation ID,
/// authenticated user ID, topic history, a topic call stack
/// (<see cref="IConversationContext.PushTopicCall"/> /
/// <see cref="IConversationContext.PopTopicCall"/>), shared key/value state, and a
/// <c>Reset()</c>. <c>ConversaCore.Agentic.DomainAgentService</c> already builds its own
/// active-topic/paused-topic/pending-subtopic bookkeeping directly on top of this context
/// today (see its <c>_context</c> field), which is exactly the composition relationship
/// this type formalizes: a <see cref="ConversationSession"/> holds one
/// <see cref="IConversationContext"/> instance internally (constructor-injected, so
/// callers can substitute a fake in tests) and forwards the parts of it that are already
/// correct and already exercised throughout <c>DomainAgentService</c> — history and the
/// call stack (see <see cref="PushTopicCall"/>, <see cref="PopTopicCall"/>,
/// <see cref="TopicCallDepth"/>, <see cref="IsTopicInCallStack"/>,
/// <see cref="TopicHistory"/>) and shared conversation values (see
/// <see cref="SetValue"/>, <see cref="GetValue{T}"/>, <see cref="TryGetValue{T}"/>,
/// <see cref="HasValue"/>) — rather than re-deriving that logic from scratch.
/// </para>
/// <para>
/// The alternative (a fully independent <c>ConversaCore.Runtime</c> implementation that
/// does not reference <see cref="IConversationContext"/> at all) was rejected because it
/// would either duplicate the call-stack/history logic verbatim (introducing a second,
/// divergence-prone copy of behavior <c>DomainAgentService</c> already relies on being
/// correct) or drop that behavior and reintroduce it later once a workflow runner needs
/// it. Target architecture section 15's current-to-target mapping table lists
/// <c>TopicRegistry</c> and <c>TopicManager</c> as the pieces that need replacing around
/// conversation state — it does not list <see cref="IConversationContext"/> itself as an
/// element to discard, which supports treating it as reusable state-tracking machinery
/// rather than API surface tied to the legacy orchestration model being replaced.
/// </para>
/// <para><b>What is genuinely new here, not delegated to <see cref="IConversationContext"/>.</b></para>
/// <para>
/// Two things this ticket adds are not present in the legacy context at all:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>A typed active topic.</b> <see cref="IConversationContext.CurrentTopicName"/> is a
/// loosely-typed string set via <see cref="IConversationContext.SetCurrentTopic"/>, with
/// no validation that the name corresponds to a real, registered topic. CC-101's
/// <see cref="TopicDescriptor"/> already gives the framework a stable, validated topic
/// identity (<see cref="TopicDescriptor.TopicId"/>) independent of display name or
/// implementing class. <see cref="ActiveTopic"/> and <see cref="SetActiveTopic"/> track
/// the active topic by <see cref="TopicDescriptor"/> reference instead, matching the
/// target architecture's separation of immutable topic definitions from mutable
/// conversation state (section 7.1) and giving future routing/runner code (CC-204,
/// CC-205) a typed value to compare against topic descriptors rather than parsing or
/// comparing display strings. Because of this, <see cref="ActiveTopic"/> deliberately does
/// <i>not</i> delegate to <see cref="IConversationContext.CurrentTopicName"/> /
/// <see cref="IConversationContext.SetCurrentTopic"/> — those legacy members are simply
/// unused by this type. Setting a non-null active topic still records the topic's ID into
/// the reused <see cref="TopicHistory"/> via
/// <see cref="IConversationContext.AddTopicToHistory"/>, so history tracking is not
/// duplicated even though active-topic tracking itself is new.
/// </description></item>
/// <item><description>
/// <b>A pending-host-interaction registry.</b> Nothing in the legacy context tracks
/// outstanding correlated host interactions — that concept (<see cref="HostInteractionResponse"/>)
/// did not exist before CC-200. See <see cref="RegisterPendingHostInteraction"/> for the
/// registry's behavior and explicit scope boundary against CC-304.
/// </description></item>
/// </list>
/// <para><b>Deliberately not exposed.</b></para>
/// <para>
/// <see cref="IConversationContext"/> also exposes <c>TopicChain</c> (a
/// <c>Queue&lt;string&gt;</c> that, by inspection, no code in
/// <c>ConversaCore.Agentic.DomainAgentService</c> ever reads or enqueues to — it is
/// exercised only by <see cref="IConversationContext"/>'s own <c>Reset()</c>/
/// <c>AddTopicToChain</c> and an unrelated <c>ResetActivity</c> that stores an unrelated
/// value under the string key <c>"TopicChain"</c>), the <c>BaseCardModel</c>-specific
/// structured-model storage members (<c>SetModel</c>/<c>GetModel</c>/<c>HasModel</c>/
/// <c>GetModels</c>/<c>RemoveModel</c>), and <c>ITerminable</c>. None of these map to a
/// target architecture section 8 "conversation session" responsibility, and the model
/// storage members in particular belong to card-authoring concerns (<c>ConversaCore.Cards</c>)
/// that are orthogonal to session bookkeeping. This type does not forward them; a caller
/// that needs the underlying <see cref="IConversationContext"/> for a legacy compatibility
/// path continues to depend on it directly and separately.
/// </para>
/// <para><b>Typed shared state is out of scope.</b></para>
/// <para>
/// Target architecture section 8 notes "new framework APIs should support typed keys or
/// typed state accessors" as a future direction; this ticket forwards the existing
/// string-keyed <see cref="SetValue"/>/<see cref="GetValue{T}"/>/<see cref="TryGetValue{T}"/>
/// shape as-is rather than introducing a typed accessor design, since that is a separate,
/// not-yet-scoped decision and not part of CC-201's task text.
/// </para>
/// </remarks>
public interface IConversationSession
{
    /// <summary>
    /// The stable identifier of the conversation this session belongs to. Delegates to
    /// the underlying <see cref="IConversationContext.ConversationId"/> and is constant
    /// for the lifetime of this scoped instance, including across <see cref="Reset"/>.
    /// </summary>
    string ConversationId { get; }

    /// <summary>
    /// The authenticated subject (user identity) this conversation belongs to. Delegates
    /// to the underlying <see cref="IConversationContext.UserId"/>. Named "subject" rather
    /// than "user ID" to match target architecture section 8's "authenticated subject"
    /// terminology; the underlying value and its resolution (currently a fixed
    /// <c>"anonymous"</c> registered at DI setup — see
    /// <c>ServiceCollectionExtensions</c>) are unchanged by this ticket.
    /// </summary>
    string Subject { get; }

    /// <summary>
    /// The topic currently active in this conversation, or <see langword="null"/> when no
    /// topic is active (for example, before the conversation starts, or immediately after
    /// <see cref="Reset"/>). Tracked by <see cref="TopicDescriptor"/> reference rather than
    /// by name or <c>ITopic</c> instance; see this interface's remarks for why.
    /// </summary>
    TopicDescriptor? ActiveTopic { get; }

    /// <summary>
    /// Sets the conversation's active topic. Passing <see langword="null"/> clears the
    /// active topic without recording anything to <see cref="TopicHistory"/>. Passing a
    /// non-null descriptor replaces any previously active topic and appends
    /// <paramref name="topic"/>'s <see cref="TopicDescriptor.TopicId"/> to
    /// <see cref="TopicHistory"/> (via the underlying
    /// <see cref="IConversationContext.AddTopicToHistory"/>), regardless of whether the
    /// same topic was already active — matching the legacy
    /// <see cref="IConversationContext.SetCurrentTopic"/> behavior this replaces, which
    /// does not deduplicate consecutive re-activations either.
    /// </summary>
    /// <param name="topic">The topic to make active, or <see langword="null"/> to clear it.</param>
    void SetActiveTopic(TopicDescriptor? topic);

    /// <summary>
    /// The topic IDs that have been active in this conversation, in activation order,
    /// including consecutive duplicates. Delegates to the underlying
    /// <see cref="IConversationContext.TopicHistory"/>. Only IDs recorded via
    /// <see cref="SetActiveTopic"/> during this session's lifetime appear here (the
    /// underlying context starts with no history of its own).
    /// </summary>
    IReadOnlyList<string> TopicHistory { get; }

    /// <summary>
    /// Pushes a topic call onto the conversation's subtopic call stack, recording that
    /// <paramref name="callingTopicId"/> is waiting on <paramref name="subTopicId"/> to
    /// complete. Delegates to the underlying
    /// <see cref="IConversationContext.PushTopicCall"/>. Topic IDs are used (rather than
    /// display names) for consistency with <see cref="ActiveTopic"/> and
    /// <see cref="TopicDescriptor.TopicId"/>.
    /// </summary>
    /// <param name="callingTopicId">The <see cref="TopicDescriptor.TopicId"/> of the topic making the call.</param>
    /// <param name="subTopicId">The <see cref="TopicDescriptor.TopicId"/> of the subtopic being called.</param>
    /// <param name="resumeData">Optional data to help resume the calling topic once the subtopic completes.</param>
    void PushTopicCall(string callingTopicId, string subTopicId, object? resumeData = null);

    /// <summary>
    /// Pops the most recent topic call from the call stack when a subtopic completes.
    /// Delegates to the underlying <see cref="IConversationContext.PopTopicCall"/>.
    /// </summary>
    /// <param name="completionData">Optional data returned by the completed subtopic.</param>
    /// <returns>Information about the call to resume, or <see langword="null"/> if the call stack is empty.</returns>
    TopicCallInfo? PopTopicCall(object? completionData = null);

    /// <summary>
    /// The current depth of the subtopic call stack (the number of nested topic calls
    /// currently outstanding). Delegates to the underlying
    /// <see cref="IConversationContext.GetTopicCallDepth"/>.
    /// </summary>
    int TopicCallDepth { get; }

    /// <summary>
    /// Determines whether <paramref name="topicId"/> is already present in the call stack,
    /// as either a calling or a called topic — used to guard against subtopic call cycles.
    /// Delegates to the underlying <see cref="IConversationContext.IsTopicInCallStack"/>.
    /// </summary>
    /// <param name="topicId">The <see cref="TopicDescriptor.TopicId"/> to check.</param>
    /// <returns><see langword="true"/> if the topic is already in the call stack; otherwise <see langword="false"/>.</returns>
    bool IsTopicInCallStack(string topicId);

    /// <summary>
    /// Sets a shared conversation-scoped value. Delegates to the underlying
    /// <see cref="IConversationContext.SetValue"/>.
    /// </summary>
    /// <param name="key">The key for the value.</param>
    /// <param name="value">The value to set.</param>
    void SetValue(string key, object value);

    /// <summary>
    /// Gets a shared conversation-scoped value. Delegates to the underlying
    /// <see cref="IConversationContext.GetValue{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key for the value.</param>
    /// <param name="defaultValue">The default value to return if the key is not found.</param>
    /// <returns>The value if found; otherwise, <paramref name="defaultValue"/>.</returns>
    T GetValue<T>(string key, T defaultValue = default!);

    /// <summary>
    /// Tries to get a shared conversation-scoped value. Delegates to the underlying
    /// <see cref="IConversationContext.TryGetValue{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The key for the value.</param>
    /// <param name="value">The value if found.</param>
    /// <returns><see langword="true"/> if the value was found; otherwise, <see langword="false"/>.</returns>
    bool TryGetValue<T>(string key, out T value);

    /// <summary>
    /// Checks whether a shared conversation-scoped value exists. Delegates to the
    /// underlying <see cref="IConversationContext.HasValue"/>.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <returns><see langword="true"/> if the key exists; otherwise, <see langword="false"/>.</returns>
    bool HasValue(string key);

    /// <summary>
    /// Registers a host interaction correlation ID as pending, meaning a topic activity is
    /// now awaiting the host's response to it (see <see cref="HostInteractionResponse.RequestId"/>
    /// and ADR-004: "Every host interaction carries a correlation ID. The response
    /// completes exactly one pending request.").
    /// </summary>
    /// <remarks>
    /// This is the minimal bookkeeping structure a future host-interaction dispatcher
    /// (CC-304) needs to look up and resolve the correct pending request and to detect
    /// late or duplicate responses structurally. It intentionally does not implement
    /// dispatch, timeouts, or typed request/response correlation — those remain CC-304's
    /// responsibility. See <see cref="TryResolvePendingHostInteraction"/> for how a
    /// duplicate or late response is expected to be detected using this registry.
    /// </remarks>
    /// <param name="requestId">
    /// The correlation ID to register as pending. Must not be null, empty, or whitespace.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="requestId"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="requestId"/> is already registered as pending. A
    /// correlation ID must be unique among currently-pending interactions; registering the
    /// same ID twice while the first registration is still outstanding most likely
    /// indicates a correlation ID collision in the caller, and is rejected immediately
    /// rather than silently discarding the first pending interaction's bookkeeping.
    /// </exception>
    void RegisterPendingHostInteraction(string requestId);

    /// <summary>
    /// Determines whether <paramref name="requestId"/> is currently registered as a
    /// pending host interaction.
    /// </summary>
    /// <param name="requestId">The correlation ID to check. Must not be null, empty, or whitespace.</param>
    /// <returns><see langword="true"/> if the ID is currently pending; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="requestId"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    bool IsHostInteractionPending(string requestId);

    /// <summary>
    /// Resolves (removes) a pending host interaction, marking it no longer outstanding.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="false"/>, rather than throwing, when
    /// <paramref name="requestId"/> is not currently registered as pending — this is the
    /// structural signal a future dispatcher (CC-304) uses to detect and reject a late
    /// response (registered, already resolved once, now arriving again) or a response to
    /// an ID this session never registered (unknown/forged correlation ID). A well-formed
    /// but unrecognized ID is a normal, expected outcome of this method, not an error
    /// condition; only a structurally invalid ID (null/empty/whitespace) throws.
    /// </remarks>
    /// <param name="requestId">The correlation ID to resolve. Must not be null, empty, or whitespace.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="requestId"/> was pending and has been
    /// removed; <see langword="false"/> if it was not currently pending (unknown, already
    /// resolved, or never registered).
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="requestId"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    bool TryResolvePendingHostInteraction(string requestId);

    /// <summary>
    /// A snapshot of every correlation ID currently registered as pending. Mutating the
    /// returned collection has no effect on this session's registry.
    /// </summary>
    IReadOnlyCollection<string> PendingHostInteractionIds { get; }

    /// <summary>
    /// Resets the session to its initial state: clears the underlying conversation
    /// context (shared values, topic history, and call stack, via
    /// <see cref="IConversationContext.Reset"/>), clears <see cref="ActiveTopic"/> back to
    /// <see langword="null"/>, and clears every pending host interaction. Conversation
    /// identity (<see cref="ConversationId"/>, <see cref="Subject"/>) is unchanged, matching
    /// <see cref="IConversationRuntime.ResetAsync"/>'s documented contract ("resetting
    /// restarts the conversation's workflow state, not its identity").
    /// </summary>
    /// <remarks>
    /// The underlying <see cref="IConversationContext.Reset"/> call clears state it owns
    /// (shared values, topic history, call stack) but has no awareness of
    /// <see cref="ActiveTopic"/> or the pending-host-interaction registry, since both are
    /// new state this type introduces. This method clears all four so that a caller only
    /// needs to call one method to fully reset session state.
    /// </remarks>
    void Reset();
}
