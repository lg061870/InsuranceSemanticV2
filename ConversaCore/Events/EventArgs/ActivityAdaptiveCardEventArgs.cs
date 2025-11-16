// === EventArgs definitions (should live in ConversaCore.Events) ===
namespace ConversaCore.Events; 
public class ActivityAdaptiveCardEventArgs : EventArgs {
    public string CardJson { get; }
    public string CardId { get; }
    public RenderMode RenderMode { get; }
    public bool IsRequired { get; }  // ✅ new property

    public ActivityAdaptiveCardEventArgs(string cardJson, string cardId, RenderMode renderMode, bool isRequired = false) {
        CardJson = cardJson;
        CardId = cardId;
        RenderMode = renderMode;
        IsRequired = isRequired;
    }
}


