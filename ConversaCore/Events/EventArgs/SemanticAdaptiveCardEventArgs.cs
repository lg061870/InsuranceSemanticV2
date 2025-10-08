namespace ConversaCore.Events;

/// <summary>
/// Event args for when Semantic Kernel produces an Adaptive Card.
/// </summary>
public class SemanticAdaptiveCardEventArgs : EventArgs {
    public string CardJson { get; }

    public SemanticAdaptiveCardEventArgs(string cardJson) {
        CardJson = cardJson;
    }
}
