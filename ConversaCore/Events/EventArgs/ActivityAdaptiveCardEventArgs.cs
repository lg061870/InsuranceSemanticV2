// === EventArgs definitions (should live in ConversaCore.Events) ===
namespace ConversaCore.Events;

/// <summary>
/// Event args raised when an activity emits or updates an adaptive card.
/// Provides enough metadata for the chat window to decide whether to
/// append or replace an existing card.
/// </summary>
public class ActivityAdaptiveCardEventArgs : EventArgs {
    public string CardJson { get; }
    public string CardId { get; }
    public RenderMode RenderMode { get; }

    public ActivityAdaptiveCardEventArgs(string cardJson, string cardId, RenderMode mode) {
        CardJson = cardJson;
        CardId = string.IsNullOrWhiteSpace(cardId) ? Guid.NewGuid().ToString() : cardId;
        RenderMode = mode;
    }
}

