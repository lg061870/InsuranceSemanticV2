namespace ConversaCore.Runtime;

/// <summary>
/// Carries the host application's answer to a previously dispatched correlated host
/// interaction request, for delivery through
/// <see cref="IConversationRuntime.RespondToHostInteractionAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>WP2-scoped placeholder (CC-200).</b> Correlated host interactions
/// (<c>HostInteractionRequest&lt;TRequest,TResponse&gt;</c>) are defined by CC-304, which
/// has not run yet; today there is no typed request/response pair, no pending-correlation
/// registry, and no timeout/cancellation/duplicate-response policy. This type carries only
/// the minimum shape needed to compile and use <see cref="IConversationRuntime"/> today: a
/// correlation identifier plus an untyped payload. WP3 will very likely replace
/// <see cref="Payload"/> with a typed response tied to the originating request's declared
/// response type, and will add the duplicate/late-response rejection behavior described in
/// target architecture section 9.2 and ADR-004. Treat this as a stopgap, not a final
/// design.
/// </para>
/// <para>
/// <see cref="RequestId"/> is the correlation ID a future host interaction dispatcher
/// assigns when it pauses a topic awaiting a host response (see ADR-004: "Every host
/// interaction carries a correlation ID. The response completes exactly one pending
/// request."). No such dispatcher exists yet in this codebase; this type only reserves the
/// shape a caller will need to supply once one does.
/// </para>
/// </remarks>
public sealed class HostInteractionResponse
{
    /// <summary>
    /// The correlation ID of the pending host interaction this response completes. Must
    /// not be null, empty, or whitespace.
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    /// The host's response payload. Untyped for now because no per-request response type
    /// is defined yet (see remarks); a future implementation will very likely replace this
    /// with a typed value validated against the originating request. May be null for
    /// interactions that carry no response data beyond acknowledgment.
    /// </summary>
    public object? Payload { get; }

    /// <summary>
    /// Creates a new host interaction response.
    /// </summary>
    /// <param name="requestId">The correlation ID of the pending host interaction. Must not be null, empty, or whitespace.</param>
    /// <param name="payload">The host's response payload, or null when the interaction carries no response data.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="requestId"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public HostInteractionResponse(string requestId, object? payload = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("Request ID must not be null, empty, or whitespace.", nameof(requestId));

        RequestId = requestId;
        Payload = payload;
    }
}
