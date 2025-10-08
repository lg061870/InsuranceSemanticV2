using ConversaCore.Events;

public class CardJsonEventArgs : EventArgs {
    public string CardJson { get; }
    public string Message { get; }
    public RenderMode RenderMode { get; }
    public string CardId { get; }
    public int? Iteration { get; }   // Optional iteration metadata

    public CardJsonEventArgs(string cardJson, string message, RenderMode mode, string cardId, int? iteration = null) {
        CardJson = cardJson;
        Message = message;
        RenderMode = mode;
        CardId = cardId;
        Iteration = iteration;
    }
}

