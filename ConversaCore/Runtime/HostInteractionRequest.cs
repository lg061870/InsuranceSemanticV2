using System.Text.Json;

namespace ConversaCore.Runtime;

/// <summary>A correlated host-interaction request with typed immutable request data.</summary>
/// <typeparam name="TRequest">The serializable request contract understood by the host.</typeparam>
/// <typeparam name="TResponse">The serializable response contract required to resume execution.</typeparam>
public sealed record HostInteractionRequest<TRequest, TResponse> : HostInteractionRequestOutput
    where TRequest : notnull
    where TResponse : notnull
{
    private readonly JsonElement _requestSnapshot;

    /// <summary>Creates and freezes a typed host-interaction request.</summary>
    public HostInteractionRequest(string conversationId, string requestId, string interactionName,
        int version, TimeSpan timeout, TRequest request, DateTimeOffset? occurredAtUtc = null)
        : base(conversationId, requestId, interactionName, version, timeout, occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        _requestSnapshot = JsonSerializer.SerializeToElement(request).Clone();
    }

    /// <summary>Gets a fresh typed copy of the frozen request payload.</summary>
    public TRequest Request => _requestSnapshot.Deserialize<TRequest>()
        ?? throw new JsonException($"Host interaction request could not be read as {typeof(TRequest).FullName}.");

    /// <summary>Gets the immutable serialized request payload.</summary>
    public JsonElement RequestSnapshot => _requestSnapshot.Clone();

    /// <summary>Gets the response contract required for this request.</summary>
    public Type ResponseType => typeof(TResponse);
}
