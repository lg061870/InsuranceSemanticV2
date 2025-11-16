using ConversaCore.Interfaces;

namespace InsuranceAgent.Services; // keep namespace for now – can move later

/// <summary>
/// Generic service that manages document embeddings and vector-database operations.
/// Handles file processing, embedding generation, and similarity search.
/// </summary>
public interface IDocumentEmbeddingService {
    string DocumentsPath { get; }

    Task<int> ProcessAllDocumentsAsync(
        string collectionName = "default_documents",
        CancellationToken cancellationToken = default);

    Task<int> ProcessFileAsync(
        string filePath,
        string collectionName = "default_documents",
        CancellationToken cancellationToken = default);

    Task<List<DocumentSearchResult>> SearchDocumentsAsync(
        string query,
        string collectionName = "default_documents",
        int maxResults = 5,
        CancellationToken cancellationToken = default);

    List<FileInfo> GetDocumentFiles();

    Task<List<string>> GetCollectionsAsync(CancellationToken cancellationToken = default);

    Task<int> GetDocumentCountAsync(
        string collectionName = "default_documents",
        CancellationToken cancellationToken = default);

    Task<bool> ClearCollectionAsync(
        string collectionName = "default_documents",
        CancellationToken cancellationToken = default);
}
