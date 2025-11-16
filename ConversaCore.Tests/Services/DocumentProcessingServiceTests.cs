using ConversaCore.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;

namespace ConversaCore.Tests.Services;

public class DocumentProcessingServiceTests : IDisposable
{
    private readonly DocumentProcessingService _service;
    private readonly Mock<ILogger<DocumentProcessingService>> _mockLogger;
    private readonly string _testDirectory;

    public DocumentProcessingServiceTests()
    {
        _mockLogger = new Mock<ILogger<DocumentProcessingService>>();
        _service = new DocumentProcessingService(_mockLogger.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), "DocumentProcessingTests", Guid.NewGuid().ToString());
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
    public void GetSupportedExtensions_ReturnsExpectedExtensions()
    {
        // Act
        var extensions = _service.GetSupportedExtensions();

        // Assert
        extensions.Should().Contain(new[] { ".txt", ".md", ".json", ".xml", ".csv", ".log", ".pdf" });
                extensions.Count.Should().BeGreaterThanOrEqualTo(5);
    }

    [Theory]
    [InlineData("test.txt", true)]
    [InlineData("test.pdf", true)]
    [InlineData("test.md", true)]
    [InlineData("test.json", true)]
    [InlineData("test.xml", true)]
    [InlineData("test.csv", true)]
    [InlineData("test.log", true)]
    [InlineData("test.docx", false)]
    [InlineData("test.exe", false)]
    [InlineData("", false)]
    public void CanProcessFile_ReturnsCorrectResult(string fileName, bool expectedResult)
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, fileName);
        if (!string.IsNullOrEmpty(fileName))
        {
            File.WriteAllText(filePath, "test content");
        }

        // Act
        var result = _service.CanProcessFile(filePath);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ExtractTextAsync_TextFile_ReturnsContent()
    {
        // Arrange
        var content = "This is a test document with multiple lines.\nSecond line here.\nThird line with more content.";
        var filePath = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = await _service.ExtractTextAsync(filePath);

        // Assert
        result.Should().Be(content);
    }

    [Fact]
    public async Task ExtractTextAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.ExtractTextAsync(filePath));
    }

    [Fact]
    public async Task ExtractTextAsync_UnsupportedFile_ThrowsNotSupportedException()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.exe");
        File.WriteAllText(filePath, "binary content");

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => _service.ExtractTextAsync(filePath));
    }

    [Fact]
    public void ChunkText_EmptyString_ReturnsEmptyList()
    {
        // Act
        var result = _service.ChunkText("");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_ShortText_ReturnsSingleChunk()
    {
        // Arrange
        var text = "This is a short text that should fit in one chunk.";

        // Act
        var result = _service.ChunkText(text, chunkSize: 1000);

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be(text);
    }

    [Fact]
    public void ChunkText_LongText_ReturnsMultipleChunks()
    {
        // Arrange
        var text = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => 
            $"This is paragraph {i}. ".PadRight(150, 'x')));

        // Act
        var result = _service.ChunkText(text, chunkSize: 300, overlapSize: 50);

        // Assert
        result.Should().HaveCountGreaterThan(1);
        result.All(chunk => chunk.Length <= 400).Should().BeTrue(); // Some tolerance for overlap
    }

    [Fact]
    public void ChunkText_WithParagraphs_SplitsOnParagraphBoundaries()
    {
        // Arrange
        var text = "First paragraph with some content.\n\nSecond paragraph with different content.\n\nThird paragraph here.";

        // Act
        var result = _service.ChunkText(text, chunkSize: 50, overlapSize: 10);

        // Assert
        result.Should().HaveCountGreaterThan(1);
        // Should preserve paragraph structure when possible
        result.Any(chunk => chunk.Contains("First paragraph")).Should().BeTrue();
        result.Any(chunk => chunk.Contains("Second paragraph")).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessFileAsync_ValidTextFile_ReturnsDocumentChunks()
    {
        // Arrange
        var content = "This is a test document.\n\nSecond paragraph with more content.\n\nThird paragraph for testing.";
        var filePath = Path.Combine(_testDirectory, "test.txt");
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = await _service.ProcessFileAsync(filePath, chunkSize: 50, overlapSize: 10);

        // Assert
        result.Should().NotBeEmpty();
        result.All(chunk => !string.IsNullOrEmpty(chunk.Id)).Should().BeTrue();
        result.All(chunk => !string.IsNullOrEmpty(chunk.Content)).Should().BeTrue();
        result.All(chunk => chunk.Metadata != null).Should().BeTrue();
        
        // Check metadata
        var firstChunk = result.First();
        firstChunk.Metadata!["source_file"].Should().Be(filePath);
        firstChunk.Metadata["file_name"].Should().Be("test.txt");
        firstChunk.Metadata["file_extension"].Should().Be(".txt");
        firstChunk.Metadata["chunk_index"].Should().Be(0);
        firstChunk.Metadata["total_chunks"].Should().Be(result.Count);
    }

    [Fact]
    public async Task ProcessDirectoryAsync_MultipleFiles_ReturnsAllChunks()
    {
        // Arrange
        var file1Path = Path.Combine(_testDirectory, "file1.txt");
        var file2Path = Path.Combine(_testDirectory, "file2.md");
        var ignoredFilePath = Path.Combine(_testDirectory, "ignored.exe");

        await File.WriteAllTextAsync(file1Path, "Content of first file.");
        await File.WriteAllTextAsync(file2Path, "Content of second file.");
        await File.WriteAllTextAsync(ignoredFilePath, "This should be ignored.");

        // Act
        var result = await _service.ProcessDirectoryAsync(_testDirectory);

        // Assert
        result.Should().NotBeEmpty();
        result.Where(chunk => chunk.Metadata!["file_name"].ToString() == "file1.txt").Should().NotBeEmpty();
        result.Where(chunk => chunk.Metadata!["file_name"].ToString() == "file2.md").Should().NotBeEmpty();
        result.Where(chunk => chunk.Metadata!["file_name"].ToString() == "ignored.exe").Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessDirectoryAsync_NonExistentDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent");

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => 
            _service.ProcessDirectoryAsync(nonExistentPath));
    }

    [Fact]
    public async Task ProcessDirectoryAsync_WithFilePattern_FiltersCorrectly()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file1.txt"), "Text file content.");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file2.pdf"), "PDF content.");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "file3.md"), "Markdown content.");

        // Act
        var result = await _service.ProcessDirectoryAsync(_testDirectory, "*.txt");

        // Assert
        result.Should().NotBeEmpty();
        result.All(chunk => chunk.Metadata!["file_extension"].ToString() == ".txt").Should().BeTrue();
    }

    [Theory]
    [InlineData(100, 20, 1)]
    [InlineData(50, 10, 2)]
    [InlineData(30, 5, 3)]
    public void ChunkText_VariousChunkSizes_ProducesExpectedNumberOfChunks(int chunkSize, int overlapSize, int expectedMinChunks)
    {
        // Arrange
        var text = "This is a longer text document that should be split into multiple chunks based on the specified chunk size. " +
                  "Each chunk should have some overlap with the previous chunk to maintain context. " +
                  "This helps ensure that important information spanning chunk boundaries is not lost.";

        // Act
        var result = _service.ChunkText(text, chunkSize, overlapSize);

        // Assert
        result.Count.Should().BeGreaterThanOrEqualTo(expectedMinChunks);

        const int buffer = 100; // allows minor overflows
        result.All(chunk => chunk.Length <= chunkSize + overlapSize + buffer)
              .Should().BeTrue("chunking should not exceed expected range by more than 100 chars");

    }
}