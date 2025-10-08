namespace ConversaCore.Events;

using ConversaCore.Models;

/// <summary>
/// Generic chat event (e.g., workflow or custom signals).
/// </summary>
public class HybridChatEventArgs : EventArgs {
    public ChatEvent ChatEvent { get; }
    public HybridChatEventArgs(ChatEvent chatEvent) => ChatEvent = chatEvent;
}
