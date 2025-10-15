using Microsoft.SemanticKernel.Memory;

namespace ConversaCore.Services;

/// <summary>
/// Service interface for vector database operations including document embedding and semantic search.
/// Supports both in-memory and persistent vector storage backends.
/// </summary>
public interface IVectorDatabaseService
{
    /// <summary>
    /// Embeds and stores a document with its metadata in the vector database.
    /// </summary>
    /// <param name="collectionName">The collection/namespace to store the document in</param>
    /// <param name="documentId">Unique identifier for the document</param>
    /// <param name="content">The text content to embed and store</param>
    /// <param name="metadata">Additional metadata about the document (filename, type, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successfully stored</returns>
    Task<bool> StoreDocumentAsync(string collectionName, string documentId, string content, 
        Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores multiple document chunks in batch for efficiency.
    /// </summary>
    /// <param name="collectionName">The collection/namespace to store documents in</param>
    /// <param name="documentChunks">List of document chunks with IDs, content, and metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of chunks successfully stored</returns>
    Task<int> StoreBatchAsync(string collectionName, List<DocumentChunk> documentChunks, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs semantic search to find the most relevant documents/chunks.
    /// </summary>
    /// <param name="collectionName">The collection to search in</param>
    /// <param name="query">The search query text</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="minRelevanceScore">Minimum relevance score (0.0 to 1.0)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of relevant document chunks ordered by relevance</returns>
    Task<List<DocumentSearchResult>> SearchAsync(string collectionName, string query, 
        int limit = 5, double minRelevanceScore = 0.7, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about available collections in the vector database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of collection names</returns>
    Task<List<string>> GetCollectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document from the vector database.
    /// </summary>
    /// <param name="collectionName">The collection containing the document</param>
    /// <param name="documentId">The document ID to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successfully removed</returns>
    Task<bool> RemoveDocumentAsync(string collectionName, string documentId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all documents from a collection.
    /// </summary>
    /// <param name="collectionName">The collection to clear</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successfully cleared</returns>
    Task<bool> ClearCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of documents in a collection.
    /// </summary>
    /// <param name="collectionName">The collection to count</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of documents in the collection</returns>
    Task<int> GetDocumentCountAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for documents by metadata criteria for duplicate detection.
    /// </summary>
    /// <param name="collectionName">The collection to search in</param>
    /// <param name="metadata">Metadata criteria to match</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of documents matching the metadata criteria</returns>
    Task<List<DocumentSearchResult>> GetDocumentsByMetadataAsync(string collectionName, 
        Dictionary<string, object> metadata, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a document chunk for batch storage operations.
/// </summary>
public record DocumentChunk(
    string Id,
    string Content,
    Dictionary<string, object>? Metadata = null);

/// <summary>
/// Represents a search result with relevance score and metadata.
/// </summary>
public record DocumentSearchResult(
    string Id,
    string Content,
    double RelevanceScore,
    Dictionary<string, object>? Metadata = null);