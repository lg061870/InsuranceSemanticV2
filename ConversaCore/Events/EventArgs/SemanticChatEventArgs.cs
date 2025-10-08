using System;
using ConversaCore.Models;

namespace ConversaCore.Events; 

/// <summary>
/// Event args for when Semantic Kernel produces a domain event (workflow signal).
/// </summary>
public class SemanticChatEventArgs : EventArgs {
    public ChatEvent ChatEvent { get; }

    public SemanticChatEventArgs(ChatEvent chatEvent) {
        ChatEvent = chatEvent;
    }
}
