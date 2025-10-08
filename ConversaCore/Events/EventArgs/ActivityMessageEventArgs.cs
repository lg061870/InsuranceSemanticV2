// === EventArgs definitions (could live in ConversaCore.Events) ===
using ConversaCore.Models;

namespace ConversaCore.Events;

public class ActivityMessageEventArgs : EventArgs {
    public ChatMessage Message { get; }
    public ActivityMessageEventArgs(ChatMessage message) => Message = message;
}
