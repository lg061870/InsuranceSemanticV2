// === EventArgs classes ===
namespace ConversaCore.Events;

public class MessageEventArgs : EventArgs {
    public string Content { get; }
    public MessageEventArgs(string content) => Content = content;
}
