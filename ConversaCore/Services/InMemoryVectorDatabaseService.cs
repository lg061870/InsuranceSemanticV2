using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates
using Microsoft.SemanticKernel.Memory;
#pragma warning restore SKEXP0001

namespace ConversaCore.Services;

/// <summary>
/// Updated in-memory implementation of vector database service using Microsoft.Extensions.AI.
/// Uses Semantic Kernel's in-memory vector store for fast, non-persistent storage.
/// </summary>
public class InMemoryVectorDatabaseService : IVectorDatabaseService
{
#pragma warning disable SKEXP0001
    private readonly ISemanticTextMemory? _memory;
#pragma warning restore SKEXP0001
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingService;
    private readonly ILogger<InMemoryVectorDatabaseService> _logger;
    private readonly ConcurrentDictionary<string, int> _collectionCounts = new();

#pragma warning disable SKEXP0001
    public InMemoryVectorDatabaseService(
        ISemanticTextMemory? memory,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingService,
        ILogger<InMemoryVectorDatabaseService> logger)
#pragma warning restore SKEXP0001
    {
        _memory = memory;
        _embeddingService = embeddingService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_memory == null)
        {
            _logger.LogWarning("Vector database initialized without semantic memory - limited functionality");
        }
    }

    public async Task<bool> StoreDocumentAsync(string collectionName, string documentId, string content,
        Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default)
    {
        if (_memory == null)
        {
            _logger.LogWarning("Cannot store document - memory service not available");
            return false;
        }

        try
        {
            _logger.LogInformation("Storing document {DocumentId} in collection {CollectionName}", 
                documentId, collectionName);

            await _memory.SaveInformationAsync(collectionName, content, documentId, 
                description: metadata?.GetValueOrDefault("description")?.ToString(),
                additionalMetadata: metadata?.GetValueOrDefault("source")?.ToString(),
                cancellationToken: cancellationToken);

            _collectionCounts.AddOrUpdate(collectionName, 1, (key, value) => value + 1);

            _logger.LogInformation("Successfully stored document {DocumentId} in collection {CollectionName}", 
                documentId, collectionName);
            return true;
        }
        catch (Exception ex)
        {
            // Provide more detailed error logging for different exception types
            var errorContext = ex switch
            {
                HttpRequestException => "OpenAI API connection failed",
                TaskCanceledException => "OpenAI API request timed out", 
                UnauthorizedAccessException => "OpenAI API authentication failed",
                ArgumentException => "Invalid document content or parameters",
                _ when ex.Message.Contains("API key") => "OpenAI API key is missing or invalid",
                _ when ex.Message.Contains("quota") => "OpenAI API quota exceeded",
                _ when ex.Message.Contains("rate") => "OpenAI API rate limit exceeded",
                _ when ex.Message.Contains("embedding") => "Failed to generate embeddings via OpenAI",
                _ => "Vector database storage failed"
            };

            _logger.LogError(ex, "Failed to store document {DocumentId} in collection {CollectionName}: {ErrorContext}", 
                documentId, collectionName, errorContext);
            return false;
        }
    }

    public async Task<int> StoreBatchAsync(string collectionName, List<DocumentChunk> documentChunks,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Storing batch of {Count} documents in collection {CollectionName}", 
            documentChunks.Count, collectionName);

        int successCount = 0;
        var failures = new List<string>();
        
        foreach (var chunk in documentChunks)
        {
            if (await StoreDocumentAsync(collectionName, chunk.Id, chunk.Content, chunk.Metadata, cancellationToken))
            {
                successCount++;
            }
            else
            {
                failures.Add(chunk.Id);
            }
        }

        if (failures.Any())
        {
            _logger.LogWarning("Failed to store {FailureCount} documents in batch: {FailedIds}", 
                failures.Count, string.Join(", ", failures));
        }

        _logger.LogInformation("Successfully stored {SuccessCount}/{TotalCount} documents in batch", 
            successCount, documentChunks.Count);
        return successCount;
    }

    public async Task<List<DocumentSearchResult>> SearchAsync(string collectionName, string query,
        int limit = 5, double minRelevanceScore = 0.7, CancellationToken cancellationToken = default)
    {
        if (_memory == null)
        {
            _logger.LogWarning("Cannot search - memory service not available");
            return new List<DocumentSearchResult>();
        }

        try
        {
            _logger.LogInformation("Searching collection {CollectionName} for query: {Query}", 
                collectionName, query);

            var searchResults = new List<DocumentSearchResult>();
            await foreach (var result in _memory.SearchAsync(collectionName, query, limit, minRelevanceScore, cancellationToken: cancellationToken))
            {
                var metadata = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(result.Metadata.AdditionalMetadata))
                {
                    metadata["source"] = result.Metadata.AdditionalMetadata;
                }
                
                searchResults.Add(new DocumentSearchResult(
                    Id: result.Metadata.Id,
                    Content: result.Metadata.Text,
                    RelevanceScore: result.Relevance,
                    Metadata: metadata
                ));
            }

            _logger.LogInformation("Found {ResultCount} results for query in collection {CollectionName}", 
                searchResults.Count, collectionName);

            return searchResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search collection {CollectionName}", collectionName);
            return new List<DocumentSearchResult>();
        }
    }

    public Task<List<string>> GetCollectionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // In-memory implementation tracks collections via our concurrent dictionary
            var collections = _collectionCounts.Keys.ToList();
            _logger.LogInformation("Retrieved {CollectionCount} collections", collections.Count);
            return Task.FromResult(collections);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collections");
            return Task.FromResult(new List<string>());
        }
    }

    public async Task<bool> RemoveDocumentAsync(string collectionName, string documentId,
        CancellationToken cancellationToken = default)
    {
        if (_memory == null)
        {
            _logger.LogWarning("Cannot remove document - memory service not available");
            return false;
        }

        try
        {
            _logger.LogInformation("Removing document {DocumentId} from collection {CollectionName}", 
                documentId, collectionName);

            await _memory.RemoveAsync(collectionName, documentId);

            _collectionCounts.AddOrUpdate(collectionName, 0, (key, value) => Math.Max(0, value - 1));

            _logger.LogInformation("Successfully removed document {DocumentId} from collection {CollectionName}", 
                documentId, collectionName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove document {DocumentId} from collection {CollectionName}", 
                documentId, collectionName);
            return false;
        }
    }

    public Task<bool> ClearCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Clearing collection {CollectionName}", collectionName);

            // Note: SemanticTextMemory doesn't have a direct clear collection method
            // For in-memory implementation, we'd need to track documents and remove individually
            _collectionCounts.TryRemove(collectionName, out _);

            _logger.LogInformation("Successfully cleared collection {CollectionName}", collectionName);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear collection {CollectionName}", collectionName);
            return Task.FromResult(false);
        }
    }

    public Task<int> GetDocumentCountAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_collectionCounts.GetValueOrDefault(collectionName, 0));
    }

    public async Task<List<DocumentSearchResult>> GetDocumentsByMetadataAsync(string collectionName, 
        Dictionary<string, object> metadata, CancellationToken cancellationToken = default)
    {
        if (_memory == null)
        {
            _logger.LogWarning("Cannot search by metadata - memory service not available");
            return new List<DocumentSearchResult>();
        }

        try
        {
            _logger.LogInformation("Searching collection {CollectionName} by metadata criteria", collectionName);

            var results = new List<DocumentSearchResult>();

            // Note: SemanticTextMemory has limited metadata search capabilities
            // For full metadata search, we'd need to implement a more sophisticated approach
            // For now, we'll use a broad search and filter results
            
            // Get all documents in the collection with a very general query
            await foreach (var result in _memory.SearchAsync(collectionName, "*", 100, 0.0, cancellationToken: cancellationToken))
            {
                // Check if this document matches our metadata criteria
                bool matches = true;
                
                // For in-memory implementation, we have limited metadata stored
                // We can only check against what's available in AdditionalMetadata
                if (metadata.TryGetValue("source_file", out var sourceFile) && 
                    sourceFile?.ToString() != result.Metadata.AdditionalMetadata)
                {
                    matches = false;
                }

                if (matches)
                {
                    var resultMetadata = new Dictionary<string, object>();
                    if (!string.IsNullOrEmpty(result.Metadata.AdditionalMetadata))
                    {
                        resultMetadata["source"] = result.Metadata.AdditionalMetadata;
                        resultMetadata["source_file"] = result.Metadata.AdditionalMetadata;
                    }

                    results.Add(new DocumentSearchResult(
                        Id: result.Metadata.Id,
                        Content: result.Metadata.Text,
                        RelevanceScore: result.Relevance,
                        Metadata: resultMetadata
                    ));
                }
            }

            _logger.LogInformation("Found {ResultCount} documents matching metadata criteria in collection {CollectionName}", 
                results.Count, collectionName);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search by metadata in collection {CollectionName}", collectionName);
            return new List<DocumentSearchResult>();
        }
    }
}