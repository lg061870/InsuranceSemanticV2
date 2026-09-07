namespace ConversaCore.Runtime;

/// <summary>
/// The framework-owned, domain-neutral facade for one conversation. This is the contract
/// CC-200 defines for the target architecture's "Framework-owned Domain Agent facade"
/// (see target architecture section 6 and ADR-001) — it replaces the current inheritance
/// model, where a domain application subclasses <c>ConversaCore.Agentic.DomainAgentService</c>
/// and overrides orchestration hooks, with composition: a domain application registers
/// topics and optional tools, and consumes this single, stable, awaitable command surface
/// instead.
/// </summary>
/// <remarks>
/// <para><b>Scope of this ticket (CC-200).</b></para>
/// <para>
/// This file defines the contract only. It intentionally does not implement
/// <see cref="IConversationRuntime"/> — that is CC-201 (the conversation session),
/// CC-205 (the workflow runner that actually executes topic activities), and the later
/// tickets that assemble a concrete implementation from them. It also does not build the
/// typed output hierarchy, host-event contracts, or tool contracts described elsewhere in
/// the target architecture; those belong to WP3 (CC-300 through CC-310) and WP4
/// (CC-400 through CC-412) respectively.
/// </para>
/// <para><b>Scoping decision: minimal placeholders, not a reduced interface.</b></para>
/// <para>
/// The work breakdown's CC-200 task text is explicit that this ticket covers "awaitable
/// start, message, card-submit, host-response, reset, and output-subscription
/// operations" — all six, as one coequal set — and the target architecture's conceptual
/// shape (section 6) already lists <see cref="SubmitCardAsync"/>,
/// <see cref="RespondToHostInteractionAsync"/>, and <see cref="Subscribe"/> alongside the
/// simpler commands. WP3's own task list assumes the full surface already exists: CC-306
/// is "Bind ConversaCore.UI to <c>IConversationRuntime</c>," which is a consumption task,
/// not a definition task. Shipping a reduced interface here (only
/// <see cref="ConversationId"/>, <see cref="StartAsync"/>, <see cref="SendMessageAsync"/>,
/// and <see cref="ResetAsync"/>) would defer real interface-shape decisions to WP3 anyway,
/// just later and under a "consume the runtime" ticket that is not scoped for API design —
/// that trades an honestly-labeled placeholder now for a bigger, less-visible surface
/// change later.
/// </para>
/// <para>
/// The alternative chosen instead: define the full six-operation surface now, and
/// introduce genuinely minimal, clearly-documented placeholder types
/// (<see cref="CardSubmission"/>, <see cref="HostInteractionResponse"/>,
/// <see cref="IConversationOutputSubscription"/>) for whatever this interface needs to
/// reference that WP3 has not designed yet. Each placeholder's XML docs say plainly that
/// WP3 (CC-300 through CC-304) will very likely refine or replace it, and none of them
/// attempt to build the <c>ConversationOutput</c> hierarchy, typed host-event contracts,
/// or correlation/timeout machinery those tickets own — they exist only so this interface
/// compiles and is usable end-to-end today, matching prior WP1 practice (CC-100, CC-103,
/// CC-105) of documenting a judgment call in XML docs rather than silently picking a side.
/// </para>
/// <para><b>Command surface, mapped from the legacy implementation.</b></para>
/// <para>
/// Each command below has a direct analog in the current
/// <c>ConversaCore.Agentic.DomainAgentService</c> (read, not modified, for this ticket),
/// confirming this is a real consolidation of an existing surface rather than a
/// speculative one:
/// </para>
/// <list type="table">
/// <listheader><term>This interface</term><description>Legacy analog</description></listheader>
/// <item><term><see cref="StartAsync"/></term><description><c>DomainAgentService.StartConversationAsync</c> / <c>OnConversationStartRequestedAsync</c></description></item>
/// <item><term><see cref="SendMessageAsync"/></term><description><c>DomainAgentService.ProcessUserMessageAsync</c> / <c>OnUserMessageReceivedAsync</c></description></item>
/// <item><term><see cref="SubmitCardAsync"/></term><description><c>DomainAgentService.HandleCardSubmitAsync(Dictionary&lt;string, object&gt;, CancellationToken)</c> / <c>OnCardSubmittedAsync</c></description></item>
/// <item><term><see cref="ResetAsync"/></term><description><c>DomainAgentService.ResetConversationAsync</c> / <c>OnConversationResetRequestedAsync</c></description></item>
/// <item><term><see cref="Subscribe"/></term><description>The public <c>event EventHandler&lt;...&gt;</c> surface (<c>ActivityMessageReady</c>, <c>ActivityAdaptiveCardReady</c>, <c>PromptInputStateChanged</c>, <c>TopicLifecycleChanged</c>, etc.), consolidated into one ordered, disposable, awaitable stream per ADR-003 instead of many independently wired <c>async void</c> handlers</description></item>
/// <item><term><see cref="RespondToHostInteractionAsync"/></term><description>No direct analog — the closest legacy behavior is <c>EventTriggerActivity</c>'s inline, uncorrelated wait-for-response handling in <c>OnCustomEventTriggered</c>, which ADR-004 identifies as unreliable (responses can be dropped, cancellation leaves stale markers). This command is new surface for the correlated request/response model CC-304 will fully define.</description></item>
/// </list>
/// <para><b>Rules that must survive any future API-name change.</b></para>
/// <para>
/// Per target architecture section 6, the exact member names may change during API
/// design, but the following do not: the implementation is supplied by ConversaCore, not
/// a domain subclass; there is exactly one scoped instance of this interface per
/// conversation or Blazor circuit (see the DI lifetimes table, target architecture section
/// 13 — <c>IConversationRuntime</c> is Scoped); every command below is asynchronous and
/// reports its own completion to the caller, with no fire-and-forget or <c>async void</c>
/// path; output is delivered only through <see cref="Subscribe"/>'s disposable
/// subscription, never through raw .NET events; and domain behavior enters exclusively
/// through registered topics, tools, and host-event contracts — never through overriding
/// or subclassing this interface's implementation.
/// </para>
/// </remarks>
public interface IConversationRuntime
{
    /// <summary>
    /// The stable identifier of the conversation this runtime instance owns. Constant for
    /// the lifetime of this scoped instance, including across <see cref="ResetAsync"/>
    /// (resetting restarts the conversation's workflow state, not its identity).
    /// </summary>
    string ConversationId { get; }

    /// <summary>
    /// Starts the conversation: activates whatever registered start topic (or start
    /// composition) applies, running any startup/compliance flow the domain application
    /// has registered. Analogous to the legacy
    /// <c>DomainAgentService.StartConversationAsync</c>, but framework-owned rather than
    /// an abstract method a domain subclass must implement.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the start operation and any activities it triggers.</param>
    /// <returns>A task that completes once the start operation has finished (the conversation may still be actively waiting for input when this completes).</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers a free-text user message to the conversation. The runtime routes the
    /// message to the active topic first (giving it first refusal per its interruption
    /// policy) and falls back to topic matching/system fallback otherwise. Analogous to
    /// the legacy <c>DomainAgentService.ProcessUserMessageAsync</c>.
    /// </summary>
    /// <param name="message">The user's message text.</param>
    /// <param name="cancellationToken">A token that cancels routing and any activity the message triggers.</param>
    /// <returns>A task that completes once the message has been routed and the resulting activity has finished or reached its next wait point.</returns>
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers an adaptive-card submission to the conversation. Analogous to the legacy
    /// <c>DomainAgentService.HandleCardSubmitAsync(Dictionary&lt;string, object&gt;,
    /// CancellationToken)</c>, but with an explicit <see cref="CardSubmission.CardId"/>
    /// instead of relying on hidden "current active card" runtime state (see
    /// <see cref="CardSubmission"/> remarks for why, and for this type's placeholder
    /// status).
    /// </summary>
    /// <param name="submission">The card ID and submitted field values.</param>
    /// <param name="cancellationToken">A token that cancels delivery and any activity the submission resumes.</param>
    /// <returns>A task that completes once the submission has been delivered and the resulting activity has finished or reached its next wait point.</returns>
    Task SubmitCardAsync(CardSubmission submission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers the host application's answer to a previously dispatched correlated host
    /// interaction request, resuming the exact topic activity that is awaiting it. This is
    /// new surface introduced for the request/response model ADR-004 and CC-304 define;
    /// see <see cref="HostInteractionResponse"/> remarks for its placeholder status and the
    /// legacy behavior it is meant to eventually replace.
    /// </summary>
    /// <param name="response">The correlation ID of the pending interaction and the host's response payload.</param>
    /// <param name="cancellationToken">A token that cancels delivery of the response.</param>
    /// <returns>A task that completes once the response has been delivered and the resumed activity has finished or reached its next wait point.</returns>
    Task RespondToHostInteractionAsync(HostInteractionResponse response, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the conversation to its initial state: clears active topic, topic stack,
    /// pending interactions, and shared conversation state, then restarts the conversation
    /// (equivalent to calling <see cref="StartAsync"/> again). Analogous to the legacy
    /// <c>DomainAgentService.ResetConversationAsync</c>, but without reflection-based state
    /// forcing (target architecture section 7.3: "Reset never uses reflection").
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the reset operation.</param>
    /// <returns>A task that completes once the conversation has been reset and restarted.</returns>
    Task ResetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a new asynchronous, disposable subscription to this conversation's output
    /// stream. Replaces the legacy public event surface
    /// (<c>ActivityMessageReady</c>, <c>ActivityAdaptiveCardReady</c>,
    /// <c>PromptInputStateChanged</c>, <c>TopicLifecycleChanged</c>, and similar events on
    /// <c>DomainAgentService</c>) with one ordered, awaitable stream per ADR-003. See
    /// <see cref="IConversationOutputSubscription"/> remarks for this subscription's
    /// placeholder output-item shape.
    /// </summary>
    /// <returns>A new subscription. The caller owns its lifetime and must dispose it (via <see cref="IAsyncDisposable"/>) when no longer needed.</returns>
    IConversationOutputSubscription Subscribe();
}
