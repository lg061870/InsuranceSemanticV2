namespace ConversaCore.Integrations.Zapier;

/// <summary>
/// Request payload for triggering a Zapier webhook
/// </summary>
public class ZapierWebhookRequest
{
    /// <summary>
    /// Data to send to Zapier - can be any JSON-serializable object
    /// </summary>
    public object Data { get; set; } = new { };

    /// <summary>
    /// Optional event type identifier
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Timestamp of the event
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Response from Zapier webhook
/// </summary>
public class ZapierWebhookResponse
{
    /// <summary>
    /// Status message from Zapier
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Request ID for tracking
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Any data returned from the Zap
    /// </summary>
    public Dictionary<string, object>? Data { get; set; }
}
