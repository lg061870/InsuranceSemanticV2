using ConversaCore.Models;

namespace ConversaCore.Events;

/// <summary>
/// Event args for when Semantic Kernel produces a plain text message.
/// </summary>
public class SemanticMessageEventArgs : EventArgs {
    public ChatMessage Message { get; }

    public SemanticMessageEventArgs(ChatMessage message) {
        Message = message;
    }
}
