namespace ConversaCore.Configuration;

/// <summary>
/// Configuration settings for vector database operations.
/// </summary>
public class VectorDatabaseConfiguration
{
    /// <summary>
    /// The vector database provider to use (InMemory, Chroma, etc.)
    /// </summary>
    public VectorDatabaseProvider Provider { get; set; } = VectorDatabaseProvider.InMemory;

    /// <summary>
    /// Connection string or configuration for the vector database.
    /// For Chroma: HTTP endpoint (e.g., "http://localhost:8000")
    /// For InMemory: Not used
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Default collection name for storing documents.
    /// </summary>
    public string DefaultCollection { get; set; } = "insurance_documents";

    /// <summary>
    /// Default chunk size for document processing (characters).
    /// </summary>
    public int DefaultChunkSize { get; set; } = 1000;

    /// <summary>
    /// Default overlap size between chunks (characters).
    /// </summary>
    public int DefaultOverlapSize { get; set; } = 200;

    /// <summary>
    /// Minimum relevance score for search results (0.0 to 1.0).
    /// </summary>
    public double MinRelevanceScore { get; set; } = 0.7;

    /// <summary>
    /// Maximum number of search results to return.
    /// </summary>
    public int MaxSearchResults { get; set; } = 5;

    /// <summary>
    /// Whether the vector database is properly configured and available.
    /// </summary>
    public bool IsConfigured => Provider switch
    {
        VectorDatabaseProvider.InMemory => true,
        VectorDatabaseProvider.Chroma => !string.IsNullOrWhiteSpace(ConnectionString),
        _ => false
    };
}

/// <summary>
/// Supported vector database providers.
/// </summary>
public enum VectorDatabaseProvider
{
    /// <summary>
    /// In-memory vector storage (non-persistent, good for development/testing).
    /// </summary>
    InMemory,

    /// <summary>
    /// Chroma vector database (can run locally or remotely).
    /// </summary>
    Chroma,

    /// <summary>
    /// Future: SQLite with vector extensions.
    /// </summary>
    SQLite,

    /// <summary>
    /// Future: Qdrant vector database.
    /// </summary>
    Qdrant
}