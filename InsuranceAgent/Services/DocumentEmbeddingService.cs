using ConversaCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Services; // keep namespace for now – can move later

/// <summary>
/// Reusable implementation that connects <see cref="IDocumentProcessingService"/> with
/// <see cref="IVectorDatabaseService"/> to embed and index documents for semantic search.
/// </summary>
public class DocumentEmbeddingService : IDocumentEmbeddingService {
    private readonly IVectorDatabaseService _vectorDb;
    private readonly IDocumentProcessingService _docProcessor;
    private readonly ILogger<DocumentEmbeddingService> _logger;
    private readonly string _documentsPath;

    public DocumentEmbeddingService(
        IVectorDatabaseService vectorDb,
        IDocumentProcessingService docProcessor,
        IConfiguration configuration,
        ILogger<DocumentEmbeddingService> logger) {
        _vectorDb = vectorDb ?? throw new ArgumentNullException(nameof(vectorDb));
        _docProcessor = docProcessor ?? throw new ArgumentNullException(nameof(docProcessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Resolve or create document directory
        var configuredPath = configuration.GetValue<string>("DocumentsPath");
        var contentRoot = configuration.GetValue<string>("ContentRoot") ?? Directory.GetCurrentDirectory();
        _documentsPath = string.IsNullOrEmpty(configuredPath)
            ? Path.Combine(contentRoot, "wwwroot", "documents")
            : configuredPath;

        if (!Directory.Exists(_documentsPath)) {
            Directory.CreateDirectory(_documentsPath);
            _logger.LogInformation("Created documents directory: {DocumentsPath}", _documentsPath);
        }
    }

    public string DocumentsPath => _documentsPath;

    public virtual async Task<int> ProcessAllDocumentsAsync(
        string collectionName = "default_documents",
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Processing all documents in directory: {DocumentsPath}", _documentsPath);

        var chunks = await _docProcessor.ProcessDirectoryAsync(_documentsPath, "*.*", cancellationToken: cancellationToken);
        if (chunks.Count == 0) {
            _logger.LogInformation("No processable documents found in directory");
            return 0;
        }

        var success = await _vectorDb.StoreBatchAsync(collectionName, chunks, cancellationToken);
        _logger.LogInformation("Processed {Success}/{Total} chunks from directory", success, chunks.Count);
        return success;
    }

    public virtual async Task<int> ProcessFileAsync(
        string filePath,
        string collectionName = "default_documents",
        CancellationToken cancellationToken = default) {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");
        if (!_docProcessor.CanProcessFile(filePath))
            throw new NotSupportedException($"Unsupported file type: {Path.GetExtension(filePath)}");

        if (await IsFileAlreadyProcessedAsync(filePath, collectionName, cancellationToken)) {
            _logger.LogInformation("File {FilePath} already processed; skipping", filePath);
            return 0;
        }

        _logger.LogInformation("Processing file: {FilePath}", filePath);

        var chunks = await _docProcessor.ProcessFileAsync(filePath, cancellationToken: cancellationToken);
        if (chunks.Count == 0) {
            _logger.LogWarning("No chunks generated for {FilePath}", filePath);
            return 0;
        }

        var success = await _vectorDb.StoreBatchAsync(collectionName, chunks, cancellationToken);
        if (success == 0)
            throw new InvalidOperationException("Vector DB failed to store document chunks (possibly embedding issue).");

        if (success < chunks.Count)
            _logger.LogWarning("Stored {Success}/{Total} chunks for {FilePath}", success, chunks.Count, filePath);

        _logger.LogInformation("File {FilePath} processed successfully", filePath);
        return success;
    }

    public virtual async Task<List<DocumentSearchResult>> SearchDocumentsAsync(
        string query,
        string collectionName = "default_documents",
        int maxResults = 5,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(query))
            return new();

        _logger.LogInformation("Searching collection {Collection} for query: {Query}", collectionName, query);
        var results = await _vectorDb.SearchAsync(collectionName, query, maxResults, cancellationToken: cancellationToken);
        _logger.LogInformation("Found {Count} results for query", results.Count);
        return results;
    }

    public List<FileInfo> GetDocumentFiles() {
        if (!Directory.Exists(_documentsPath)) return new();
        var supported = _docProcessor.GetSupportedExtensions();
        return Directory.GetFiles(_documentsPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => supported.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.Name)
            .ToList();
    }

    public async Task<List<string>> GetCollectionsAsync(CancellationToken cancellationToken = default)
        => await _vectorDb.GetCollectionsAsync(cancellationToken);

    public async Task<int> GetDocumentCountAsync(
        string collectionName = "default_documents",
        CancellationToken cancellationToken = default)
        => await _vectorDb.GetDocumentCountAsync(collectionName, cancellationToken);

    public async Task<bool> ClearCollectionAsync(
        string collectionName = "default_documents",
        CancellationToken cancellationToken = default)
        => await _vectorDb.ClearCollectionAsync(collectionName, cancellationToken);

    // ---------- Internal Helpers ----------

    protected virtual async Task<bool> IsFileAlreadyProcessedAsync(
        string filePath,
        string collectionName,
        CancellationToken cancellationToken) {
        try {
            var info = new FileInfo(filePath);
            var meta = new Dictionary<string, object> {
                ["file_name"] = info.Name,
                ["source_file"] = filePath
            };

            var docs = await _vectorDb.GetDocumentsByMetadataAsync(collectionName, meta, cancellationToken);
            if (!docs.Any()) return false;

            foreach (var d in docs) {
                if (d.Metadata is null) continue;
                if (d.Metadata.TryGetValue("last_modified", out var lmObj) &&
                    d.Metadata.TryGetValue("file_size", out var szObj) &&
                    lmObj is DateTime lm &&
                    szObj is long size &&
                    Math.Abs((lm - info.LastWriteTimeUtc).TotalSeconds) < 2 &&
                    size == info.Length)
                    return true;
            }

            await RemoveOutdatedFileChunksAsync(filePath, collectionName, cancellationToken);
            return false;
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Error checking if file already processed: {File}", filePath);
            return false;
        }
    }

    protected virtual async Task RemoveOutdatedFileChunksAsync(
        string filePath,
        string collectionName,
        CancellationToken cancellationToken) {
        var meta = new Dictionary<string, object> { ["source_file"] = filePath };
        var outdated = await _vectorDb.GetDocumentsByMetadataAsync(collectionName, meta, cancellationToken);
        foreach (var d in outdated)
            await _vectorDb.RemoveDocumentAsync(collectionName, d.Id, cancellationToken);

        if (outdated.Any())
            _logger.LogInformation("Removed {Count} outdated chunks for {File}", outdated.Count, filePath);
    }
}
