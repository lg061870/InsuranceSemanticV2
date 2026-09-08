using System.Text.Json;

namespace ConversaCore.Runtime;

/// <summary>A versioned one-way host notification with a typed immutable payload snapshot.</summary>
/// <typeparam name="TPayload">The serializable domain contract understood by the host.</typeparam>
/// <remarks>The supplied payload is serialized when this output is created. Reading
/// <see cref="Payload"/> produces a fresh value from that snapshot, so later mutation of the
/// caller's object—or of a previously returned value—cannot change the dispatched output.</remarks>
public sealed record HostNotification<TPayload> : HostNotificationOutput
    where TPayload : notnull
{
    private readonly JsonElement _payloadSnapshot;

    /// <summary>Creates a typed host notification and freezes its payload.</summary>
    public HostNotification(string conversationId, string eventName, int version, TPayload payload,
        DateTimeOffset? occurredAtUtc = null) : base(conversationId, eventName, version, occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _payloadSnapshot = JsonSerializer.SerializeToElement(payload).Clone();
    }

    /// <summary>Gets a fresh typed copy of the frozen payload.</summary>
    /// <exception cref="JsonException">Thrown when the snapshot cannot be reconstructed as <typeparamref name="TPayload"/>.</exception>
    public TPayload Payload => _payloadSnapshot.Deserialize<TPayload>()
        ?? throw new JsonException($"Host notification payload could not be read as {typeof(TPayload).FullName}.");

    /// <summary>Gets an immutable JSON snapshot suitable for transport or diagnostics.</summary>
    public JsonElement PayloadSnapshot => _payloadSnapshot.Clone();
}
