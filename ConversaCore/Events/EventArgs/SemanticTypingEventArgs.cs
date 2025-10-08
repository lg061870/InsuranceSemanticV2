namespace ConversaCore.Events; 

public class SemanticTypingEventArgs : EventArgs {
    public bool IsTyping { get; }
    public SemanticTypingEventArgs(bool isTyping) => IsTyping = isTyping;
}
