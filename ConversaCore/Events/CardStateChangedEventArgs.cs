namespace ConversaCore.Events; 
public class CardStateChangedEventArgs : EventArgs {
    public string CardId { get; }
    public CardState State { get; }

    public CardStateChangedEventArgs(string cardId, CardState state) {
        CardId = cardId;
        State = state;
    }
}