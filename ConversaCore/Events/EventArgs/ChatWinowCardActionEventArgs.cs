// === EventArgs classes ===
namespace ConversaCore.Events;

public class ChatWinowCardActionEventArgs : EventArgs {
    public string ActionId { get; }
    public ChatWinowCardActionEventArgs(string actionId) => ActionId = actionId;
}
