using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;

namespace ConversaCore.Services;

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

/// <summary>
/// Basic implementation of document processing service.
/// Currently supports TXT files with future extensibility for PDF processing.
/// </summary>
public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly ILogger<DocumentProcessingService> _logger;
    private readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".csv", ".log"
        // PDF support would be added here with appropriate library (e.g., iTextSharp, PdfPig)
        // ".pdf"
    };

    public DocumentProcessingService(ILogger<DocumentProcessingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        if (!CanProcessFile(filePath))
            throw new NotSupportedException($"File type not supported: {Path.GetExtension(filePath)}");

        try
        {
            _logger.LogInformation("Extracting text from file: {FilePath}", filePath);

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            return extension switch
            {
                ".txt" or ".md" or ".log" => await File.ReadAllTextAsync(filePath, cancellationToken),
                ".json" or ".xml" or ".csv" => await File.ReadAllTextAsync(filePath, cancellationToken),
                // ".pdf" => await ExtractPdfTextAsync(filePath, cancellationToken), // Future implementation
                _ => throw new NotSupportedException($"File type not supported: {extension}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from file: {FilePath}", filePath);
            throw;
        }
    }

    public List<string> ChunkText(string text, int chunkSize = 1000, int overlapSize = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var chunks = new List<string>();
        
        // First, try to split by paragraphs (double newlines or similar)
        var paragraphs = Regex.Split(text, @"\r?\n\s*\r?\n")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var currentChunk = new StringBuilder();
        
        foreach (var paragraph in paragraphs)
        {
            // If adding this paragraph would exceed chunk size, save current chunk
            if (currentChunk.Length > 0 && currentChunk.Length + paragraph.Length > chunkSize)
            {
                chunks.Add(currentChunk.ToString().Trim());
                
                // Start new chunk with overlap from previous chunk if specified
                var overlapText = GetOverlapText(currentChunk.ToString(), overlapSize);
                currentChunk.Clear();
                if (!string.IsNullOrWhiteSpace(overlapText))
                {
                    currentChunk.Append(overlapText);
                    currentChunk.Append("\n\n");
                }
            }

            // If single paragraph is larger than chunk size, split it further
            if (paragraph.Length > chunkSize)
            {
                var sentences = SplitIntoSentences(paragraph);
                foreach (var sentence in sentences)
                {
                    if (currentChunk.Length + sentence.Length > chunkSize && currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        var overlapText = GetOverlapText(currentChunk.ToString(), overlapSize);
                        currentChunk.Clear();
                        if (!string.IsNullOrWhiteSpace(overlapText))
                        {
                            currentChunk.Append(overlapText);
                            currentChunk.Append(" ");
                        }
                    }
                    currentChunk.Append(sentence);
                    currentChunk.Append(" ");
                }
            }
            else
            {
                if (currentChunk.Length > 0)
                    currentChunk.Append("\n\n");
                currentChunk.Append(paragraph);
            }
        }

        // Add remaining text as final chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        _logger.LogInformation("Split text into {ChunkCount} chunks (chunk size: {ChunkSize}, overlap: {OverlapSize})", 
            chunks.Count, chunkSize, overlapSize);

        return chunks;
    }

    public async Task<List<DocumentChunk>> ProcessFileAsync(string filePath, int chunkSize = 1000,
        int overlapSize = 200, CancellationToken cancellationToken = default)
    {
        try
        {
            var text = await ExtractTextAsync(filePath, cancellationToken);
            var chunks = ChunkText(text, chunkSize, overlapSize);
            
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var fileInfo = new FileInfo(filePath);
            var fileSize = fileInfo.Length;
            var lastModified = fileInfo.LastWriteTimeUtc;
            
            var documentChunks = chunks.Select((chunk, index) => new DocumentChunk(
                Id: $"{fileName}_chunk_{index:D3}",
                Content: chunk,
                Metadata: new Dictionary<string, object>
                {
                    ["source_file"] = filePath,
                    ["file_name"] = Path.GetFileName(filePath),
                    ["file_extension"] = extension,
                    ["file_size"] = fileSize,
                    ["last_modified"] = lastModified,
                    ["chunk_index"] = index,
                    ["total_chunks"] = chunks.Count,
                    ["processed_date"] = DateTimeOffset.UtcNow
                }
            )).ToList();

            _logger.LogInformation("Processed file {FilePath} into {ChunkCount} chunks", filePath, documentChunks.Count);
            return documentChunks;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<List<DocumentChunk>> ProcessDirectoryAsync(string directoryPath, string filePattern = "*.*",
        int chunkSize = 1000, int overlapSize = 200, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        _logger.LogInformation("Processing directory: {DirectoryPath} with pattern: {FilePattern}", 
            directoryPath, filePattern);

        var allChunks = new List<DocumentChunk>();
        var files = Directory.GetFiles(directoryPath, filePattern, SearchOption.TopDirectoryOnly)
            .Where(CanProcessFile)
            .ToList();

        _logger.LogInformation("Found {FileCount} processable files in directory", files.Count);

        foreach (var file in files)
        {
            try
            {
                var fileChunks = await ProcessFileAsync(file, chunkSize, overlapSize, cancellationToken);
                allChunks.AddRange(fileChunks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping file due to processing error: {FilePath}", file);
            }
        }

        _logger.LogInformation("Processed directory {DirectoryPath}: {TotalChunks} chunks from {FileCount} files", 
            directoryPath, allChunks.Count, files.Count);

        return allChunks;
    }

    public List<string> GetSupportedExtensions()
    {
        return _supportedExtensions.ToList();
    }

    public bool CanProcessFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        var extension = Path.GetExtension(filePath);
        return !string.IsNullOrWhiteSpace(extension) && _supportedExtensions.Contains(extension);
    }

    private static string GetOverlapText(string text, int overlapSize)
    {
        if (overlapSize <= 0 || text.Length <= overlapSize)
            return "";

        var overlapText = text.Substring(text.Length - overlapSize);
        
        // Try to find a good break point (space, period, newline)
        var breakPoints = new[] { ". ", "\n", " " };
        foreach (var breakPoint in breakPoints)
        {
            var lastIndex = overlapText.LastIndexOf(breakPoint);
            if (lastIndex > overlapSize / 2) // Don't use break point if it's too early
            {
                return overlapText.Substring(lastIndex + breakPoint.Length);
            }
        }

        return overlapText;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        // Simple sentence splitting - could be enhanced with more sophisticated NLP
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return sentences;
    }

    // Future PDF processing method placeholder
    // private async Task<string> ExtractPdfTextAsync(string filePath, CancellationToken cancellationToken)
    // {
    //     // Implementation would use a PDF library like:
    //     // - iTextSharp (now iText 7)
    //     // - PdfPig
    //     // - Syncfusion PDF
    //     throw new NotImplementedException("PDF processing not yet implemented");
    // }
}