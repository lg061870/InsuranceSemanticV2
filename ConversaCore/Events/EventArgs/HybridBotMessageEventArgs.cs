namespace ConversaCore.Events;

using ConversaCore.Models;

/// <summary>
/// Raised when the HybridChatService has a new bot message to show.
/// </summary>
public class HybridBotMessageEventArgs : EventArgs {
    public ChatMessage Message { get; }
    public HybridBotMessageEventArgs(ChatMessage message) => Message = message;
}
