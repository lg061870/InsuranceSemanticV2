namespace ConversaCore.Events; 
public class AdaptiveCardInputCollectedEventArgs : EventArgs {
    public Dictionary<string, object> Data { get; }
    public AdaptiveCardInputCollectedEventArgs(Dictionary<string, object> data) {
        Data = data;
    }
}