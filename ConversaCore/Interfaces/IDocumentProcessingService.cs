namespace ConversaCore.Interfaces;

/// <summary>
/// Service interface for document processing operations including text extraction,
/// chunking, and file management for vector database ingestion.
/// </summary>
public interface IDocumentProcessingService
{
    /// <summary>
    /// Extracts text content from a file (supports PDF, TXT, etc.)
    /// </summary>
    /// <param name="filePath">Path to the file to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted text content</returns>
    Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Splits text into chunks suitable for embedding and vector storage.
    /// </summary>
    /// <param name="text">The text to chunk</param>
    /// <param name="chunkSize">Maximum size of each chunk in characters</param>
    /// <param name="overlapSize">Number of characters to overlap between chunks</param>
    /// <returns>List of text chunks</returns>
    List<string> ChunkText(string text, int chunkSize = 1000, int overlapSize = 200);

    /// <summary>
    /// Processes a file and creates document chunks ready for vector storage.
    /// </summary>
    /// <param name="filePath">Path to the file to process</param>
    /// <param name="chunkSize">Maximum size of each chunk</param>
    /// <param name="overlapSize">Overlap between chunks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of document chunks with metadata</returns>
    Task<List<DocumentChunk>> ProcessFileAsync(string filePath, int chunkSize = 1000, 
        int overlapSize = 200, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes multiple files in a directory.
    /// </summary>
    /// <param name="directoryPath">Directory containing files to process</param>
    /// <param name="filePattern">File pattern to match (e.g., "*.pdf")</param>
    /// <param name="chunkSize">Maximum size of each chunk</param>
    /// <param name="overlapSize">Overlap between chunks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all document chunks from all processed files</returns>
    Task<List<DocumentChunk>> ProcessDirectoryAsync(string directoryPath, string filePattern = "*.*",
        int chunkSize = 1000, int overlapSize = 200, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets supported file extensions for text extraction.
    /// </summary>
    /// <returns>List of supported file extensions</returns>
    List<string> GetSupportedExtensions();

    /// <summary>
    /// Validates if a file can be processed.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if file can be processed</returns>
    bool CanProcessFile(string filePath);
}
