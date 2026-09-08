using System.Text.Json;

namespace ConversaCore.Runtime;

/// <summary>Immutable response envelope for a correlated host interaction.</summary>
/// <remarks>The payload is frozen as JSON when constructed. The coordinator deserializes it as
/// the response type declared by the matching <see cref="HostInteractionRequest{TRequest,TResponse}"/>.</remarks>
public class HostInteractionResponse
{
    private readonly JsonElement _payloadSnapshot;
    private readonly Type? _payloadType;

    /// <summary>Creates a response envelope from a serializable payload.</summary>
    public HostInteractionResponse(string requestId, object? payload = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        RequestId = requestId;
        _payloadType = payload?.GetType();
        _payloadSnapshot = payload is null
            ? JsonSerializer.SerializeToElement<object?>(null)
            : JsonSerializer.SerializeToElement(payload, payload.GetType()).Clone();
    }

    /// <summary>Gets the correlation identifier copied from the request.</summary>
    public string RequestId { get; }

    /// <summary>Gets a fresh copy of the payload using its construction-time runtime type.</summary>
    public object? Payload => _payloadType is null ? null : _payloadSnapshot.Deserialize(_payloadType);

    /// <summary>Gets the immutable serialized response payload.</summary>
    public JsonElement PayloadSnapshot => _payloadSnapshot.Clone();

    internal TResponse ReadPayload<TResponse>() where TResponse : notnull =>
        _payloadSnapshot.Deserialize<TResponse>()
        ?? throw new JsonException($"Host interaction response could not be read as {typeof(TResponse).FullName}.");
}

/// <summary>A strongly typed host response that remains compatible with the runtime envelope.</summary>
public sealed class HostInteractionResponse<TResponse> : HostInteractionResponse
    where TResponse : notnull
{
    /// <summary>Creates and freezes a typed response.</summary>
    public HostInteractionResponse(string requestId, TResponse payload) : base(requestId, payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
    }

    /// <summary>Gets a fresh typed copy of the frozen response payload.</summary>
    public new TResponse Payload => ReadPayload<TResponse>();
}
