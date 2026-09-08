using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConversaCore.Runtime;

/// <summary>
/// Immutable compatibility payload produced for a legacy <c>EventTriggerActivity</c>.
/// </summary>
/// <remarks>
/// New domain integrations should define their own typed host notification or interaction contracts.
/// This envelope exists only so existing event-trigger activities can cross the runtime boundary
/// without exposing mutable workflow state.
/// </remarks>
public sealed record LegacyEventTriggerPayload
{
    private readonly JsonElement _dataSnapshot;

    /// <summary>Creates an immutable compatibility payload from its serialized data.</summary>
    [JsonConstructor]
    public LegacyEventTriggerPayload(string activityId, JsonElement data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityId);
        ActivityId = activityId;
        _dataSnapshot = data.Clone();
    }

    /// <summary>Creates an immutable compatibility payload from arbitrary legacy event data.</summary>
    public static LegacyEventTriggerPayload Create(string activityId, object? data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityId);
        var snapshot = data is null
            ? JsonSerializer.SerializeToElement<object?>(null)
            : JsonSerializer.SerializeToElement(data, data.GetType()).Clone();
        return new LegacyEventTriggerPayload(activityId, snapshot);
    }

    /// <summary>Gets the identifier of the legacy activity that produced the event.</summary>
    public string ActivityId { get; }

    /// <summary>Gets an immutable JSON snapshot of the legacy event data.</summary>
    public JsonElement Data => _dataSnapshot.Clone();
}
