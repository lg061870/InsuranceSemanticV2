namespace ConversaCore.Models {
    public class ChatMessage {
        public string Content { get; set; } = string.Empty;
        public bool IsFromUser { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsAdaptiveCard { get; set; }
        public string? AdaptiveCardJson { get; set; }
        public string? CardId { get; set; }
    }
}
