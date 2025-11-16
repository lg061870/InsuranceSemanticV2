namespace ConversaCore.Events; 
public class PromptInputStateChangedEventArgs : EventArgs {
    public bool IsEnabled { get; }
    public string CardId { get; }

    public PromptInputStateChangedEventArgs(bool isEnabled, string cardId) {
        IsEnabled = isEnabled;
        CardId = cardId;
    }
}
