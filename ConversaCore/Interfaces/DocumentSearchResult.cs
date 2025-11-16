namespace ConversaCore.Interfaces;

/// <summary>
/// Represents a semantic search result with relevance score.
/// </summary>
public record DocumentSearchResult(
    string Id,
    string Content,
    double RelevanceScore,
    Dictionary<string, object>? Metadata = null);