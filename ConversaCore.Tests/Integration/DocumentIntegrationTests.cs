using ConversaCore.Interfaces;
using ConversaCore.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ConversaCore.Tests.Integration;

public class DocumentIntegrationTests : IDisposable
{
    private readonly Mock<IVectorDatabaseService> _mockVectorService;
    private readonly Mock<ILogger<DocumentProcessingService>> _mockLogger;
    private readonly DocumentProcessingService _documentService;
    private readonly string _testDirectory;

    public DocumentIntegrationTests()
    {
        _mockVectorService = new Mock<IVectorDatabaseService>();
        _mockLogger = new Mock<ILogger<DocumentProcessingService>>();
        
        _documentService = new DocumentProcessingService(_mockLogger.Object);
        
        // Create a temporary test directory
        _testDirectory = Path.Combine(Path.GetTempPath(), $"doc_integration_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Integration_ProcessTextFile_ExtractAndChunk()
    {
        // Arrange
        var testContent = "This is a test document for integration testing. " +
                         "It contains multiple sentences to test chunking functionality. " +
                         "The document processing service should handle this correctly. " +
                         "We expect it to be split into appropriate chunks.";
        
        var testFile = Path.Combine(_testDirectory, "integration_test.txt");
        await File.WriteAllTextAsync(testFile, testContent);

        // Act - Extract text
        var extractedText = await _documentService.ExtractTextAsync(testFile);

        // Assert - Text extraction
        extractedText.Should().NotBeNullOrEmpty();
        extractedText.Should().Contain("integration testing");

        // Act - Chunk text
        var chunks = _documentService.ChunkText(extractedText, chunkSize: 100, overlapSize: 20);

        // Assert - Text chunking
        chunks.Should().NotBeEmpty();
        chunks.Should().HaveCountGreaterThan(1);
        chunks.All(c => c.Length <= 100).Should().BeTrue();

        // Act - Process file (extract + chunk + metadata)
        var documentChunks = await _documentService.ProcessFileAsync(testFile, chunkSize: 100, overlapSize: 20);

        // Assert - Complete processing
        documentChunks.Should().NotBeEmpty();
        documentChunks.Should().HaveCount(chunks.Count);
        documentChunks.All(dc => dc.Content.Length <= 100).Should().BeTrue();
        documentChunks.All(dc => dc.Metadata!.ContainsKey("source_file")).Should().BeTrue();
        documentChunks.All(dc => dc.Metadata!.ContainsKey("file_name")).Should().BeTrue();
    }

    [Fact]
    public async Task Integration_ProcessMultipleFiles_HandlesDirectory()
    {
        // Arrange - Create multiple test files
        var files = new Dictionary<string, string>
        {
            ["file1.txt"] = "This is the first test file with some content.",
            ["file2.md"] = "# Markdown File\nThis is a markdown document with **bold** text.",
            ["file3.json"] = """{"name": "test", "description": "JSON test file"}""",
            ["ignored.xyz"] = "This file should be ignored due to unsupported extension."
        };

        foreach (var kvp in files)
        {
            var filePath = Path.Combine(_testDirectory, kvp.Key);
            await File.WriteAllTextAsync(filePath, kvp.Value);
        }

        // Act
        var allChunks = await _documentService.ProcessDirectoryAsync(_testDirectory, filePattern: "*.*");

        // Assert
        allChunks.Should().NotBeEmpty();
        
        // Should process .txt, .md, .json but not .xyz
        var processedSources = allChunks
            .SelectMany(c => c.Metadata?.Values ?? Enumerable.Empty<object>())
            .OfType<string>()
            .Where(s => s.Contains("file"))
            .Distinct()
            .ToList();

        processedSources.Should().Contain(s => s.Contains("file1.txt"));
        processedSources.Should().Contain(s => s.Contains("file2.md"));
        processedSources.Should().Contain(s => s.Contains("file3.json"));
        processedSources.Should().NotContain(s => s.Contains("ignored.xyz"));
    }

    [Fact]
    public async Task Integration_VectorDatabaseWorkflow_StoreAndSearch() {
        // Arrange
        var collectionName = "integration_test_collection";
        var testContent = "Machine learning and artificial intelligence are transforming industries.";

        var testFile = Path.Combine(_testDirectory, "ml_document.txt");
        await File.WriteAllTextAsync(testFile, testContent);

        // Process document
        var chunks = await _documentService.ProcessFileAsync(testFile, chunkSize: 200);

        // Setup mock vector database
        _mockVectorService
            .Setup(x => x.StoreBatchAsync(
                collectionName,
                It.IsAny<List<DocumentChunk>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks.Count);

        var searchResults = new List<DocumentSearchResult>
        {
        new("chunk_1", chunks[0].Content, 0.95, chunks[0].Metadata)
    };

        // ✅ Updated: allow any topK / minScore / cancellation token
        _mockVectorService
            .Setup(x => x.SearchAsync(
                collectionName,
                "machine learning",
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        _mockVectorService
            .Setup(x => x.GetDocumentCountAsync(collectionName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks.Count);

        // Act - Store documents
        var storedCount = await _mockVectorService.Object.StoreBatchAsync(collectionName, chunks);

        // Assert - Storage
        storedCount.Should().Be(chunks.Count);

        // Act - Search documents
        var results = await _mockVectorService.Object.SearchAsync(collectionName, "machine learning");

        // Assert - Search
        results.Should().NotBeEmpty();
        results.First().Content.Should().Contain("Machine learning");

        // Act - Get collection stats
        var count = await _mockVectorService.Object.GetDocumentCountAsync(collectionName);

        // Assert - Stats
        count.Should().Be(chunks.Count);

        // ✅ Updated verifications
        _mockVectorService.Verify(
            x => x.StoreBatchAsync(collectionName, chunks, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockVectorService.Verify(
            x => x.SearchAsync(
                collectionName,
                "machine learning",
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockVectorService.Verify(
            x => x.GetDocumentCountAsync(collectionName, It.IsAny<CancellationToken>()),
            Times.Once);
    }


    [Fact]
    public async Task Integration_FileValidation_ChecksSupportedExtensions()
    {
        // Arrange
        var supportedFiles = new[]
        {
            "document.txt",
            "document.pdf", 
            "document.md",
            "document.json",
            "document.xml",
            "document.csv",
            "document.log"
        };

        var unsupportedFiles = new[]
        {
            "document.exe",
            "document.dll",
            "document.bin",
            "document.docx", // Not supported yet
            "document.xlsx"  // Not supported yet
        };

        // Act & Assert - Supported files
        foreach (var file in supportedFiles)
        {
            var testPath = Path.Combine(_testDirectory, file);
            _documentService.CanProcessFile(testPath).Should().BeTrue($"{file} should be supported");
        }

        // Act & Assert - Unsupported files
        foreach (var file in unsupportedFiles)
        {
            var testPath = Path.Combine(_testDirectory, file);
            _documentService.CanProcessFile(testPath).Should().BeFalse($"{file} should not be supported");
        }

        // Act & Assert - Get supported extensions
        var extensions = _documentService.GetSupportedExtensions();
        extensions.Should().Contain(".txt");
        extensions.Should().Contain(".pdf");
        extensions.Should().Contain(".md");
        extensions.Should().NotContain(".exe");
        extensions.Should().NotContain(".dll");
    }

    [Fact]
    public async Task Integration_ChunkingAlgorithm_PreservesContext()
    {
        // Arrange
        var longText = string.Join(" ", Enumerable.Range(1, 100).Select(i => 
            $"This is sentence number {i} in a very long document."));

        // Act - Chunk with overlap
        var chunksWithOverlap = _documentService.ChunkText(longText, chunkSize: 200, overlapSize: 50);

        // Assert - Overlap preservation
        chunksWithOverlap.Should().HaveCountGreaterThan(1);
        
        for (int i = 1; i < chunksWithOverlap.Count; i++)
        {
            var previousChunk = chunksWithOverlap[i - 1];
            var currentChunk = chunksWithOverlap[i];
            
            // Check that there's some overlap between consecutive chunks
            var previousEnd = previousChunk.Substring(Math.Max(0, previousChunk.Length - 100));
            var overlap = previousEnd.Substring(0, Math.Min(50, previousEnd.Length));
            
            // Simple overlap check - current chunk should contain some portion of the previous chunk's end
            var hasOverlap = currentChunk.Contains(overlap.Substring(0, Math.Min(20, overlap.Length))) ||
                           previousEnd.Contains(currentChunk.Substring(0, Math.Min(20, currentChunk.Length)));
            hasOverlap.Should().BeTrue($"Chunks {i-1} and {i} should have some overlap");
        }
    }

    [Theory]
    [InlineData(".txt", "Plain text content")]
    [InlineData(".md", "# Markdown Header\n\nMarkdown content")]
    [InlineData(".json", """{"key": "value", "array": [1, 2, 3]}""")]
    [InlineData(".xml", "<root><item>XML content</item></root>")]
    [InlineData(".csv", "Name,Age,City\nJohn,30,NYC\nJane,25,LA")]
    [InlineData(".log", "[2024-01-01 10:00:00] INFO: Log entry")]
    public async Task Integration_FileTypeProcessing_HandlesVariousFormats(string extension, string content)
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, $"test{extension}");
        await File.WriteAllTextAsync(testFile, content);

        // Act
        var extractedText = await _documentService.ExtractTextAsync(testFile);
        var chunks = await _documentService.ProcessFileAsync(testFile);

        // Assert
        extractedText.Should().NotBeNullOrEmpty($"Should extract text from {extension} files");
        chunks.Should().NotBeEmpty($"Should create chunks from {extension} files");
        chunks.All(c => !string.IsNullOrWhiteSpace(c.Content)).Should().BeTrue();
        chunks.All(c => c.Metadata != null).Should().BeTrue();
        chunks.All(c => c.Metadata!.ContainsKey("file_extension")).Should().BeTrue();
        chunks.All(c => c.Metadata!["file_extension"].ToString() == extension).Should().BeTrue();
    }

    [Fact]
    public async Task Integration_ErrorHandling_HandlesInvalidFiles()
    {
        // Arrange - Non-existent file
        var nonExistentFile = Path.Combine(_testDirectory, "does_not_exist.txt");

        // Act & Assert - Should throw for non-existent file
        await _documentService.Invoking(s => s.ExtractTextAsync(nonExistentFile))
            .Should().ThrowAsync<FileNotFoundException>();

        await _documentService.Invoking(s => s.ProcessFileAsync(nonExistentFile))
            .Should().ThrowAsync<FileNotFoundException>();

        // Arrange - Empty file
        var emptyFile = Path.Combine(_testDirectory, "empty.txt");
        await File.WriteAllTextAsync(emptyFile, string.Empty);

        // Act & Assert - Should handle empty file gracefully
        var emptyText = await _documentService.ExtractTextAsync(emptyFile);
        emptyText.Should().BeEmpty();

        var emptyChunks = await _documentService.ProcessFileAsync(emptyFile);
        emptyChunks.Should().BeEmpty();
    }
}