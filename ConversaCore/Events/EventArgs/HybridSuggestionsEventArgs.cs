namespace ConversaCore.Events;

/// <summary>
/// Raised when the HybridChatService wants to update the suggestion chips.
/// </summary>
public class HybridSuggestionsEventArgs : EventArgs {
    public IReadOnlyList<string> Suggestions { get; }
    public HybridSuggestionsEventArgs(IEnumerable<string> suggestions) =>
        Suggestions = suggestions.ToList();
}
