namespace ConversaCore.Models; 
public class ChatMessage {
    public string Content { get; set; } = string.Empty;
    public bool IsFromUser { get; set; }
    public DateTime Timestamp { get; set; }

    // Existing adaptive card props
    public bool IsAdaptiveCard { get; set; }
    public string? AdaptiveCardJson { get; set; }
    public string? CardId { get; set; }

    // ✅ NEW property — used by HybridCardStateChanged
    public bool IsActive { get; set; } = true;
}
