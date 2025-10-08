// === EventArgs classes ===
namespace ConversaCore.Events; 

public class ChatWindowBotMessageEventArgs : EventArgs {
    public string Content { get; }
    public ChatWindowBotMessageEventArgs(string content) => Content = content;
}