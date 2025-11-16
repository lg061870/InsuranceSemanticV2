using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.Interfaces;

/// <summary>
/// Defines a general contract for a pluggable vector database service,
/// including document storage, retrieval, and embedding operations.
/// </summary>
public interface IVectorDatabaseService {
    // ----------------------------------------------------------------------
    // DOCUMENT STORAGE
    // ----------------------------------------------------------------------

    /// <summary>
    /// Stores a single document embedding in the vector database.
    /// </summary>
    Task<bool> StoreDocumentAsync(
        string collectionName,
        string documentId,
        string content,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores multiple document chunks in batch for improved efficiency.
    /// </summary>
    Task<int> StoreBatchAsync(
        string collectionName,
        List<DocumentChunk> documentChunks,
        CancellationToken cancellationToken = default);

    // ----------------------------------------------------------------------
    // SEARCH
    // ----------------------------------------------------------------------

    /// <summary>
    /// Performs semantic search to retrieve relevant document chunks.
    /// </summary>
    Task<List<DocumentSearchResult>> SearchAsync(
        string collectionName,
        string query,
        int limit = 5,
        double minRelevanceScore = 0.7,
        CancellationToken cancellationToken = default);

    // ----------------------------------------------------------------------
    // COLLECTION MANAGEMENT
    // ----------------------------------------------------------------------

    Task<List<string>> GetCollectionsAsync(
        CancellationToken cancellationToken = default);

    Task<bool> RemoveDocumentAsync(
        string collectionName,
        string documentId,
        CancellationToken cancellationToken = default);

    Task<bool> ClearCollectionAsync(
        string collectionName,
        CancellationToken cancellationToken = default);

    Task<int> GetDocumentCountAsync(
        string collectionName,
        CancellationToken cancellationToken = default);

    Task<List<DocumentSearchResult>> GetDocumentsByMetadataAsync(
        string collectionName,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default);

    // ----------------------------------------------------------------------
    // EMBEDDING GENERATION
    // ----------------------------------------------------------------------

    /// <summary>
    /// Generates a single embedding vector for the provided text.
    /// </summary>
    /// <param name="text">Raw text to embed semantically.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>Embedding vector represented as an array of floats.</returns>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}
