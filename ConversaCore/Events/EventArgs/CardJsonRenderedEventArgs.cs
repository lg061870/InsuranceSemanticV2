// === EventArgs classes ===
namespace ConversaCore.Events;

/// <summary>
/// Event args for when a card has been rendered on the client side (chat window).
/// </summary>
public class CardJsonRenderedEventArgs : EventArgs {
    public string CardJson { get; }
    public DateTime RenderedAt { get; }

    public CardJsonRenderedEventArgs(string cardJson) {
        CardJson = cardJson;
        RenderedAt = DateTime.UtcNow;
    }
}
