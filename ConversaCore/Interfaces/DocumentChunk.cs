namespace ConversaCore.Interfaces;

/// <summary>
/// Represents a single logical chunk of a document (for embeddings).
/// </summary>
public record DocumentChunk(
    string Id,
    string Content,
    Dictionary<string, object>? Metadata = null);
