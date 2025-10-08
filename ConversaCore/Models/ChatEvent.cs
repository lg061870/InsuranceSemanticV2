namespace ConversaCore.Models; 
/// <summary>
/// A simple event wrapper used to surface activity payloads
/// in a normalized format for UI or service consumers.
/// </summary>
/// 
public class ChatEvent {
    /// <summary>
    /// Event type (usually the payload type name).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Arbitrary payload (e.g., message text, serialized model, etc.).
    /// </summary>
    public object? Payload { get; set; }
}

