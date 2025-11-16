namespace ConversaCore.Events; 
public class HybridAdaptiveCardEventArgs : EventArgs {
    public string CardJson { get; }
    public string CardId { get; }
    public RenderMode RenderMode { get; }

    public HybridAdaptiveCardEventArgs(string cardJson, string cardId, RenderMode mode) {
        CardJson = cardJson;
        CardId = string.IsNullOrWhiteSpace(cardId) ? Guid.NewGuid().ToString() : cardId;
        RenderMode = mode;
    }
}
