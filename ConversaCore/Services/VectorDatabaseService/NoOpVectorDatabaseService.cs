using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConversaCore.Services;

/// <summary>
/// A no-op (mock) implementation of <see cref="IVectorDatabaseService"/>.
/// This allows the application to run without a real vector database backend.
/// All methods return safe defaults and log calls for debugging.
/// </summary>
public class NoOpVectorDatabaseService : IVectorDatabaseService {
    private readonly ILogger<NoOpVectorDatabaseService>? _logger;

    public NoOpVectorDatabaseService(ILogger<NoOpVectorDatabaseService>? logger = null) {
        _logger = logger;
        _logger?.LogInformation("⚠️ Using NoOpVectorDatabaseService — vector retrieval disabled.");
    }

    // ----------------------------------------------------------------------
    // DOCUMENT STORAGE
    // ----------------------------------------------------------------------

    public Task<bool> StoreDocumentAsync(
        string collectionName,
        string documentId,
        string content,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default) {
        _logger?.LogDebug("[NoOpVectorDB] StoreDocumentAsync(collection: {Collection}, id: {Id})", collectionName, documentId);
        return Task.FromResult(true);
    }

    public Task<int> StoreBatchAsync(
        string collectionName,
        List<DocumentChunk> documentChunks,
        CancellationToken cancellationToken = default) {
        int count = documentChunks?.Count ?? 0;
        _logger?.LogDebug("[NoOpVectorDB] StoreBatchAsync(collection: {Collection}, count: {Count})", collectionName, count);
        return Task.FromResult(count);
    }

    // ----------------------------------------------------------------------
    // SEARCH
    // ----------------------------------------------------------------------

    public Task<List<DocumentSearchResult>> SearchAsync(
        string collectionName,
        string query,
        int limit = 5,
        double minRelevanceScore = 0.7,
        CancellationToken cancellationToken = default) {
        _logger?.LogDebug("[NoOpVectorDB] SearchAsync(collection: {Collection}, query: {Query})", collectionName, query);
        return Task.FromResult(new List<DocumentSearchResult>());
    }

    // ----------------------------------------------------------------------
    // COLLECTION MANAGEMENT
    // ----------------------------------------------------------------------

    public Task<List<string>> GetCollectionsAsync(CancellationToken cancellationToken = default) {
        _logger?.LogDebug("[NoOpVectorDB] GetCollectionsAsync()");
        return Task.FromResult(new List<string>());
    }

    public Task<bool> RemoveDocumentAsync(
        string collectionName,
        string documentId,
        CancellationToken cancellationToken = default) {
        _logger?.LogDebug("[NoOpVectorDB] RemoveDocumentAsync(collection: {Collection}, id: {Id})", collectionName, documentId);
        return Task.FromResult(true);
    }

    public Task<bool> ClearCollectionAsync(
        string collectionName,
        CancellationToken cancellationToken = default) {
        _logger?.LogDebug("[NoOpVectorDB] ClearCollectionAsync(collection: {Collection})", collectionName);
        return Task.FromResult(true);
    }

    public Task<int> GetDocumentCountAsync(
        string collectionName,
        CancellationToken cancellationToken = default) {
        _logger?.LogDebug("[NoOpVectorDB] GetDocumentCountAsync(collection: {Collection})", collectionName);
        return Task.FromResult(0);
    }

    public Task<List<DocumentSearchResult>> GetDocumentsByMetadataAsync(
        string collectionName,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default) {
        _logger?.LogDebug("[NoOpVectorDB] GetDocumentsByMetadataAsync(collection: {Collection})", collectionName);
        return Task.FromResult(new List<DocumentSearchResult>());
    }

    // ----------------------------------------------------------------------
    // EMBEDDINGS (added to satisfy new interface)
    // ----------------------------------------------------------------------

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) {
        _logger?.LogDebug("[NoOpVectorDB] GenerateEmbeddingAsync(text length: {Length})", text?.Length ?? 0);
        return Task.FromResult(new float[1536]); // Return zero-vector
    }

    public Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default) {
        _logger?.LogDebug("[NoOpVectorDB] GenerateBatchEmbeddingsAsync(count: {Count})", texts?.Count ?? 0);
        return Task.FromResult(new List<float[]>(texts?.Count ?? 0));
    }
}
