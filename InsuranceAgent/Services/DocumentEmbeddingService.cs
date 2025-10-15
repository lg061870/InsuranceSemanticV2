using ConversaCore.Services;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Services;

/// <summary>
/// Service for managing document embeddings and vector database operations.
/// Handles file processing, embedding generation, and search capabilities.
/// </summary>
public interface IDocumentEmbeddingService
{
    /// <summary>
    /// Gets the configured documents directory path.
    /// </summary>
    string DocumentsPath { get; }

    /// <summary>
    /// Processes all files in the documents directory and stores them in the vector database.
    /// </summary>
    /// <param name="collectionName">Collection to store documents in</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of documents processed successfully</returns>
    Task<int> ProcessAllDocumentsAsync(string collectionName = "insurance_documents", CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a specific file and stores it in the vector database.
    /// </summary>
    /// <param name="filePath">Path to the file to process</param>
    /// <param name="collectionName">Collection to store document in</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of chunks created from the file</returns>
    Task<int> ProcessFileAsync(string filePath, string collectionName = "insurance_documents", CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for documents similar to the given query.
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="collectionName">Collection to search in</param>
    /// <param name="maxResults">Maximum number of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of search results</returns>
    Task<List<DocumentSearchResult>> SearchDocumentsAsync(string query, string collectionName = "insurance_documents", 
        int maxResults = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all files in the documents directory.
    /// </summary>
    /// <returns>List of file information</returns>
    List<FileInfo> GetDocumentFiles();

    /// <summary>
    /// Gets information about collections in the vector database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of collection names</returns>
    Task<List<string>> GetCollectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of documents in a collection.
    /// </summary>
    /// <param name="collectionName">Collection name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of documents</returns>
    Task<int> GetDocumentCountAsync(string collectionName = "insurance_documents", CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all documents from a collection.
    /// </summary>
    /// <param name="collectionName">Collection to clear</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful</returns>
    Task<bool> ClearCollectionAsync(string collectionName = "insurance_documents", CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of document embedding service using ConversaCore vector database services.
/// </summary>
public class DocumentEmbeddingService : IDocumentEmbeddingService
{
    private readonly IVectorDatabaseService _vectorDb;
    private readonly IDocumentProcessingService _docProcessor;
    private readonly ILogger<DocumentEmbeddingService> _logger;
    private readonly string _documentsPath;

    public DocumentEmbeddingService(
        IVectorDatabaseService vectorDb,
        IDocumentProcessingService docProcessor,
        IConfiguration configuration,
        ILogger<DocumentEmbeddingService> logger)
    {
        _vectorDb = vectorDb ?? throw new ArgumentNullException(nameof(vectorDb));
        _docProcessor = docProcessor ?? throw new ArgumentNullException(nameof(docProcessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Get documents path from configuration or use default under wwwroot
        var configuredPath = configuration.GetValue<string>("DocumentsPath");
        if (string.IsNullOrEmpty(configuredPath))
        {
            // Default to wwwroot/documents in the InsuranceAgent project
            var contentRoot = configuration.GetValue<string>("ContentRoot") ?? Directory.GetCurrentDirectory();
            _documentsPath = Path.Combine(contentRoot, "wwwroot", "documents");
        }
        else
        {
            _documentsPath = configuredPath;
        }

        // Ensure documents directory exists
        if (!Directory.Exists(_documentsPath))
        {
            Directory.CreateDirectory(_documentsPath);
            _logger.LogInformation("Created documents directory: {DocumentsPath}", _documentsPath);
        }
    }

    public string DocumentsPath => _documentsPath;

    public async Task<int> ProcessAllDocumentsAsync(string collectionName = "insurance_documents", CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing all documents in directory: {DocumentsPath}", _documentsPath);

        try
        {
            var chunks = await _docProcessor.ProcessDirectoryAsync(_documentsPath, "*.*", cancellationToken: cancellationToken);
            
            if (chunks.Count == 0)
            {
                _logger.LogInformation("No processable documents found in directory");
                return 0;
            }

            var successCount = await _vectorDb.StoreBatchAsync(collectionName, chunks, cancellationToken);
            
            _logger.LogInformation("Processed {SuccessCount}/{TotalCount} document chunks from directory", 
                successCount, chunks.Count);

            return successCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process documents in directory: {DocumentsPath}", _documentsPath);
            throw;
        }
    }

    public async Task<int> ProcessFileAsync(string filePath, string collectionName = "insurance_documents", CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        if (!_docProcessor.CanProcessFile(filePath))
            throw new NotSupportedException($"File type not supported: {Path.GetExtension(filePath)}");

        // Check if file is already processed and up-to-date
        if (await IsFileAlreadyProcessedAsync(filePath, collectionName, cancellationToken))
        {
            _logger.LogInformation("File {FilePath} is already processed and up-to-date, skipping", filePath);
            return 0;
        }

        _logger.LogInformation("Processing file: {FilePath}", filePath);

        try
        {
            var chunks = await _docProcessor.ProcessFileAsync(filePath, cancellationToken: cancellationToken);
            
            if (chunks.Count == 0)
            {
                _logger.LogWarning("No chunks generated for file: {FilePath}", filePath);
                return 0;
            }

            var successCount = await _vectorDb.StoreBatchAsync(collectionName, chunks, cancellationToken);

            if (successCount == 0)
            {
                // No chunks were stored successfully - likely an OpenAI/embedding issue
                _logger.LogError("Vector database failed to store any chunks for file: {FilePath}. This may indicate OpenAI API issues.", filePath);
                throw new InvalidOperationException("Vector database could not store document chunks. This may be due to OpenAI API issues, missing API key, or network connectivity problems. Check the application logs for more details.");
            }
            else if (successCount < chunks.Count)
            {
                // Partial failure
                _logger.LogWarning("Vector database stored only {SuccessCount}/{TotalCount} chunks for file: {FilePath}", 
                    successCount, chunks.Count, filePath);
            }

            _logger.LogInformation("Processed file {FilePath}: {SuccessCount}/{TotalCount} chunks stored", 
                filePath, successCount, chunks.Count);

            return successCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<List<DocumentSearchResult>> SearchDocumentsAsync(string query, string collectionName = "insurance_documents", 
        int maxResults = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<DocumentSearchResult>();

        _logger.LogInformation("Searching documents for query: {Query}", query);

        try
        {
            var results = await _vectorDb.SearchAsync(collectionName, query, maxResults, cancellationToken: cancellationToken);
            
            _logger.LogInformation("Found {ResultCount} search results for query", results.Count);
            
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search documents for query: {Query}", query);
            return new List<DocumentSearchResult>();
        }
    }

    public List<FileInfo> GetDocumentFiles()
    {
        if (!Directory.Exists(_documentsPath))
            return new List<FileInfo>();

        try
        {
            var supportedExtensions = _docProcessor.GetSupportedExtensions();
            var files = Directory.GetFiles(_documentsPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .Select(file => new FileInfo(file))
                .OrderBy(file => file.Name)
                .ToList();

            _logger.LogDebug("Found {FileCount} supported files in documents directory", files.Count);
            
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document files from directory: {DocumentsPath}", _documentsPath);
            return new List<FileInfo>();
        }
    }

    public async Task<List<string>> GetCollectionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _vectorDb.GetCollectionsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collections from vector database");
            return new List<string>();
        }
    }

    public async Task<int> GetDocumentCountAsync(string collectionName = "insurance_documents", CancellationToken cancellationToken = default)
    {
        try
        {
            return await _vectorDb.GetDocumentCountAsync(collectionName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document count for collection: {CollectionName}", collectionName);
            return 0;
        }
    }

    public async Task<bool> ClearCollectionAsync(string collectionName = "insurance_documents", CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Clearing collection: {CollectionName}", collectionName);
            var result = await _vectorDb.ClearCollectionAsync(collectionName, cancellationToken);
            
            if (result)
            {
                _logger.LogInformation("Successfully cleared collection: {CollectionName}", collectionName);
            }
            else
            {
                _logger.LogWarning("Failed to clear collection: {CollectionName}", collectionName);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear collection: {CollectionName}", collectionName);
            return false;
        }
    }

    /// <summary>
    /// Checks if a file has already been processed and is up-to-date in the vector database.
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <param name="collectionName">Collection to check in</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if file is already processed and current</returns>
    private async Task<bool> IsFileAlreadyProcessedAsync(string filePath, string collectionName, CancellationToken cancellationToken)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var fileName = fileInfo.Name;
            var lastModified = fileInfo.LastWriteTimeUtc;
            var fileSize = fileInfo.Length;

            // Search for existing documents with matching file metadata
            var searchMetadata = new Dictionary<string, object>
            {
                ["file_name"] = fileName,
                ["source_file"] = filePath
            };

            var existingDocs = await _vectorDb.GetDocumentsByMetadataAsync(collectionName, searchMetadata, cancellationToken);
            
            if (!existingDocs.Any())
            {
                _logger.LogDebug("No existing documents found for file: {FilePath}", filePath);
                return false;
            }

            // Check if any existing document has same or newer modification date and size
            foreach (var doc in existingDocs)
            {
                if (doc.Metadata != null && 
                    doc.Metadata.TryGetValue("last_modified", out var lastModObj) &&
                    doc.Metadata.TryGetValue("file_size", out var fileSizeObj))
                {
                    if (lastModObj is DateTime lastModDateTime && fileSizeObj is long docFileSize)
                    {
                        var docLastModified = DateTime.SpecifyKind(lastModDateTime, DateTimeKind.Utc);
                        
                        // File is current if modification time and size match
                        if (Math.Abs((docLastModified - lastModified).TotalSeconds) < 2 && docFileSize == fileSize)
                        {
                            _logger.LogDebug("File {FilePath} is already processed and current (mod: {ModTime}, size: {Size})", 
                                filePath, lastModified, fileSize);
                            return true;
                        }
                    }
                }
            }

            _logger.LogDebug("File {FilePath} has been modified since last processing, will reprocess", filePath);
            
            // Remove outdated chunks before reprocessing
            await RemoveOutdatedFileChunksAsync(filePath, collectionName, cancellationToken);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if file is already processed: {FilePath}, will process anyway", filePath);
            return false;
        }
    }

    /// <summary>
    /// Removes outdated chunks for a file that's being reprocessed.
    /// </summary>
    /// <param name="filePath">Path to the file whose chunks should be removed</param>
    /// <param name="collectionName">Collection to remove chunks from</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task RemoveOutdatedFileChunksAsync(string filePath, string collectionName, CancellationToken cancellationToken)
    {
        try
        {
            var searchMetadata = new Dictionary<string, object>
            {
                ["source_file"] = filePath
            };

            var outdatedDocs = await _vectorDb.GetDocumentsByMetadataAsync(collectionName, searchMetadata, cancellationToken);
            
            foreach (var doc in outdatedDocs)
            {
                await _vectorDb.RemoveDocumentAsync(collectionName, doc.Id, cancellationToken);
            }

            if (outdatedDocs.Any())
            {
                _logger.LogInformation("Removed {Count} outdated chunks for file: {FilePath}", outdatedDocs.Count, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove outdated chunks for file: {FilePath}", filePath);
        }
    }
}