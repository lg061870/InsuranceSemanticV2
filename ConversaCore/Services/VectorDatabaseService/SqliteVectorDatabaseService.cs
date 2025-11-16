using ConversaCore.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;

namespace ConversaCore.Services;

/// <summary>
/// SQLite vector database service using Microsoft.SemanticKernel.Connectors.SqliteVec (1.66+)
/// and OpenAI embeddings via Microsoft.Extensions.AI.
/// </summary>
public class SqliteVectorDatabaseService : IVectorDatabaseService {
    private readonly SqliteVectorStore _store;
    private readonly VectorStoreCollectionDefinition _definition;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<SqliteVectorDatabaseService> _logger;

    public SqliteVectorDatabaseService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<SqliteVectorDatabaseService> logger,
        string dbPath = "vectorstore.db") {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _store = new SqliteVectorStore($"Data Source={dbPath}");

        _definition = new VectorStoreCollectionDefinition {
            Properties =
            {
                new VectorStoreKeyProperty("Key", typeof(string)),
                new VectorStoreDataProperty("Content", typeof(string)),
                new VectorStoreDataProperty("Metadata", typeof(Dictionary<string, object>)),
                new VectorStoreVectorProperty("Embedding", typeof(float[]), dimensions: 1536)
            }
        };

        _logger.LogInformation("SqliteVectorDatabaseService initialized with IEmbeddingGenerator<{Input},{Embedding}>",
            nameof(String), nameof(Embedding<float>));
    }

    // ----------------------------------------------------------------------
    // DOCUMENT STORAGE
    // ----------------------------------------------------------------------

    public async Task<bool> StoreDocumentAsync(
        string collectionName,
        string documentId,
        string content,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default) {
        await EnsureCollectionExistsAsync(collectionName, cancellationToken);
        var embedding = await GenerateEmbeddingAsync(content, cancellationToken);

        var collection = _store.GetDynamicCollection(collectionName, _definition);
        var record = new Dictionary<string, object?> {
            ["Key"] = documentId,
            ["Content"] = content,
            ["Metadata"] = metadata ?? new Dictionary<string, object>(),
            ["Embedding"] = embedding
        };

        await collection.UpsertAsync(record, cancellationToken);
        _logger.LogDebug("Stored document {Id} in collection {Collection}", documentId, collectionName);
        return true;
    }

    public async Task<int> StoreBatchAsync(
        string collectionName,
        List<DocumentChunk> documentChunks,
        CancellationToken cancellationToken = default) {
        if (documentChunks.Count == 0) return 0;
        await EnsureCollectionExistsAsync(collectionName, cancellationToken);

        var collection = _store.GetDynamicCollection(collectionName, _definition);
        var texts = documentChunks.Select(dc => dc.Content).ToList();
        var embeddings = await GenerateBatchEmbeddingsAsync(texts, cancellationToken);

        var records = documentChunks.Select((dc, i) => new Dictionary<string, object?> {
            ["Key"] = dc.Id,
            ["Content"] = dc.Content,
            ["Metadata"] = dc.Metadata ?? new Dictionary<string, object>(),
            ["Embedding"] = embeddings.ElementAtOrDefault(i) ?? GeneratePlaceholderEmbedding(dc.Content)
        }).ToList();

        await collection.UpsertAsync(records, cancellationToken);
        _logger.LogDebug("Batch stored {Count} records into {Collection}", records.Count, collectionName);
        return records.Count;
    }

    // ----------------------------------------------------------------------
    // SEARCH
    // ----------------------------------------------------------------------

    public async Task<List<DocumentSearchResult>> SearchAsync(
        string collectionName,
        string query,
        int limit = 5,
        double minRelevanceScore = 0.7,
        CancellationToken cancellationToken = default) {
        var results = new List<DocumentSearchResult>();
        if (!await _store.CollectionExistsAsync(collectionName, cancellationToken))
            return results;

        var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);
        var collection = _store.GetDynamicCollection(collectionName, _definition);

        var options = new VectorSearchOptions<Dictionary<string, object?>> { IncludeVectors = false };
        await foreach (var match in collection.SearchAsync(queryEmbedding, limit * 2, options, cancellationToken)) {
            if (match.Score is double score && score >= minRelevanceScore) {
                var record = match.Record;
                results.Add(new DocumentSearchResult(
                    record["Key"]?.ToString() ?? string.Empty,
                    record["Content"]?.ToString() ?? string.Empty,
                    score,
                    record["Metadata"] as Dictionary<string, object> ?? new Dictionary<string, object>()));

                if (results.Count >= limit) break;
            }
        }

        return results;
    }

    // ----------------------------------------------------------------------
    // COLLECTION MANAGEMENT
    // ----------------------------------------------------------------------

    public async Task<List<string>> GetCollectionsAsync(CancellationToken ct = default) {
        var names = new List<string>();
        await foreach (var n in _store.ListCollectionNamesAsync(ct)) names.Add(n);
        return names;
    }

    public async Task<bool> RemoveDocumentAsync(string collectionName, string documentId, CancellationToken ct = default) {
        var collection = _store.GetDynamicCollection(collectionName, _definition);
        await collection.DeleteAsync(documentId, ct);
        return true;
    }

    public async Task<bool> ClearCollectionAsync(string collectionName, CancellationToken ct = default) {
        if (!await _store.CollectionExistsAsync(collectionName, ct)) return true;

        var collection = _store.GetDynamicCollection(collectionName, _definition);
        var dummy = new float[1536];
        var options = new VectorSearchOptions<Dictionary<string, object?>> { IncludeVectors = false };

        var ids = new List<string>();
        await foreach (var match in collection.SearchAsync(dummy, int.MaxValue, options, ct)) {
            if (match.Record.TryGetValue("Key", out var idObj) && idObj is string id)
                ids.Add(id);
        }

        foreach (var id in ids)
            await collection.DeleteAsync(id, ct);

        return true;
    }

    public async Task<int> GetDocumentCountAsync(string collectionName, CancellationToken ct = default) {
        if (!await _store.CollectionExistsAsync(collectionName, ct)) return 0;

        var collection = _store.GetDynamicCollection(collectionName, _definition);
        var dummy = new float[1536];
        var options = new VectorSearchOptions<Dictionary<string, object?>> { IncludeVectors = false };

        int count = 0;
        await foreach (var _ in collection.SearchAsync(dummy, int.MaxValue, options, ct)) count++;
        return count;
    }

    public async Task<List<DocumentSearchResult>> GetDocumentsByMetadataAsync(
        string collectionName,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default) {
        var results = new List<DocumentSearchResult>();
        if (!await _store.CollectionExistsAsync(collectionName, cancellationToken))
            return results;

        var collection = _store.GetDynamicCollection(collectionName, _definition);
        var dummy = new float[1536];
        var options = new VectorSearchOptions<Dictionary<string, object?>> { IncludeVectors = false };

        await foreach (var match in collection.SearchAsync(dummy, int.MaxValue, options, cancellationToken)) {
            if (match.Record.TryGetValue("Metadata", out var metaObj) &&
                metaObj is Dictionary<string, object> recordMeta) {
                bool matches = metadata.All(kv =>
                    recordMeta.TryGetValue(kv.Key, out var val) &&
                    val?.ToString() == kv.Value?.ToString());

                if (matches) {
                    results.Add(new DocumentSearchResult(
                        match.Record["Key"]?.ToString() ?? string.Empty,
                        match.Record["Content"]?.ToString() ?? string.Empty,
                        match.Score ?? 0.0,
                        recordMeta));
                }
            }
        }

        return results;
    }

    // ----------------------------------------------------------------------
    // EMBEDDING GENERATION (PUBLIC INTERFACE)
    // ----------------------------------------------------------------------

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default) {
        try {
            var result = await _embeddingGenerator.GenerateAsync(new[] { text }, cancellationToken: ct);
            return result.Single().Vector.ToArray();
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to generate embedding, falling back to placeholder");
            return GeneratePlaceholderEmbedding(text);
        }
    }

    // ----------------------------------------------------------------------
    // INTERNAL HELPERS
    // ----------------------------------------------------------------------

    private async Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts, CancellationToken ct) {
        try {
            var result = await _embeddingGenerator.GenerateAsync(texts, cancellationToken: ct);
            return result.Select(e => e.Vector.ToArray()).ToList();
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Batch embedding failed, reverting to individual generation");
            var list = new List<float[]>();
            foreach (var text in texts)
                list.Add(await GenerateEmbeddingAsync(text, ct));
            return list;
        }
    }

    private static float[] GeneratePlaceholderEmbedding(string text) {
        unchecked {
            int hash = text.Aggregate(0, (acc, c) => acc + c);
            var vec = new float[1536];
            for (int i = 0; i < vec.Length; i++)
                vec[i] = ((hash + i) % 100) / 100f;
            return vec;
        }
    }

    private async Task EnsureCollectionExistsAsync(string name, CancellationToken ct) {
        if (!await _store.CollectionExistsAsync(name, ct)) {
            var col = _store.GetDynamicCollection(name, _definition);
            await col.EnsureCollectionExistsAsync(ct);
        }
    }
}
