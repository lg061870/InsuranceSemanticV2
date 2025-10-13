// === EventArgs classes ===
namespace ConversaCore.Events;

public class ChatWindowCardActionEventArgs : EventArgs {
    public string ActionId { get; }
    public ChatWindowCardActionEventArgs(string actionId) => ActionId = actionId;
}
