namespace ConversaCore.Events; 
public class SemanticTypingIndicatorEventArgs : System.EventArgs {
    public bool IsTyping { get; }

    public SemanticTypingIndicatorEventArgs(bool isTyping) {
        IsTyping = isTyping;
    }
}
