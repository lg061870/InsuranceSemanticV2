using ConversaCore.Events;

public class CardJsonEventArgs : EventArgs {
    public string CardJson { get; }
    public string Message { get; }
    public RenderMode RenderMode { get; }
    public string CardId { get; }
    public int? Iteration { get; } // Optional iteration metadata

    // 🆕 NEW: Marks if the card is required before continuing
    public bool IsRequired { get; }

    // --- Existing constructor remains compatible ---
    public CardJsonEventArgs(
        string cardJson,
        string message,
        RenderMode mode,
        string cardId,
        int? iteration = null,
        bool isRequired = false) {
        CardJson = cardJson;
        Message = message;
        RenderMode = mode;
        CardId = cardId;
        Iteration = iteration;
        IsRequired = isRequired; // default false = no behavior change
    }
}
