namespace ConversaCore.Events; 
public class HybridCardStateChangedEventArgs : EventArgs {
    public string CardId { get; }
    public CardState State { get; }

    public HybridCardStateChangedEventArgs(string cardId, CardState state) {
        CardId = cardId;
        State = state;
    }
}

public enum CardState {
    Active,
    ReadOnly
}