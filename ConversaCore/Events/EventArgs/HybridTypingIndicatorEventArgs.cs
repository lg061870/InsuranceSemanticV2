namespace ConversaCore.Events;

/// <summary>
/// Raised when typing indicator should start/stop.
/// </summary>
public class HybridTypingIndicatorEventArgs : EventArgs {
    public bool IsTyping { get; }
    public HybridTypingIndicatorEventArgs(bool isTyping) => IsTyping = isTyping;
}
