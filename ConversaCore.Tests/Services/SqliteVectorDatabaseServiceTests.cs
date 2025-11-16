#pragma warning disable SKEXP0010
using ConversaCore.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Microsoft.Extensions.Configuration.Json;
using ConversaCore.Interfaces;

namespace ConversaCore.Tests.Services; 
public class VectorDatabaseServiceTests {
    private readonly SqliteVectorDatabaseService _service;

    public VectorDatabaseServiceTests() {
        // Mock IEmbeddingGenerator<string, Embedding<float>> and logger
        var mockEmbeddingGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        var mockLogger = new Mock<ILogger<SqliteVectorDatabaseService>>();

        // Return deterministic embeddings (float[1536] with all 0.42f)
        mockEmbeddingGenerator
            .Setup(x => x.GenerateAsync(It.IsAny<IEnumerable<string>>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> inputs, object? _, CancellationToken _) => {
                var embeddings = inputs
                    .Select(_ => new Embedding<float>(Enumerable.Repeat(0.42f, 1536).ToArray()))
                    .ToList();
                return new GeneratedEmbeddings<Embedding<float>>(embeddings);
            });

        _service = new SqliteVectorDatabaseService(
            mockEmbeddingGenerator.Object,
            mockLogger.Object,
            "test_vectorstore.db");
    }

    [Fact]
    public async Task StoreDocumentAsync_ValidInput_ReturnsTrue() {
        var result = await _service.StoreDocumentAsync(
            "test_collection",
            "doc_1",
            "This is a test document content.",
            new Dictionary<string, object> { ["source"] = "test" });

        result.Should().BeTrue();
    }

    [Fact]
    public async Task StoreBatchAsync_MultipleDocuments_ReturnsCorrectCount() {
        var docs = new List<DocumentChunk>
        {
            new("doc_1", "First document content"),
            new("doc_2", "Second document content"),
            new("doc_3", "Third document content")
        };

        var result = await _service.StoreBatchAsync("batch_test", docs);
        result.Should().Be(3);
    }

    [Fact]
    public async Task SearchAsync_ReturnsRelevantResults() {
        await _service.StoreDocumentAsync("search_test", "1", "Machine learning is fascinating.");
        var results = await _service.SearchAsync("search_test", "machine learning");

        results.Should().NotBeEmpty();
        results.All(r => r.Content.Contains("Machine")).Should().BeTrue();
    }

    [Fact]
    public async Task GetCollectionsAsync_ReturnsList() {
        var collections = await _service.GetCollectionsAsync();
        collections.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ClearCollectionAsync_RemovesDocuments() {
        var name = "clear_test";
        await _service.StoreDocumentAsync(name, "1", "Doc to remove");
        var before = await _service.GetDocumentCountAsync(name);
        before.Should().BeGreaterThan(0);

        await _service.ClearCollectionAsync(name);
        var after = await _service.GetDocumentCountAsync(name);
        after.Should().Be(0);
    }

    [Fact]
    public async Task GetDocumentsByMetadataAsync_FiltersCorrectly() {
        var name = "meta_test";
        await _service.StoreDocumentAsync(name, "meta_doc", "AI content",
            new Dictionary<string, object> { ["category"] = "tech" });

        var results = await _service.GetDocumentsByMetadataAsync(name,
            new Dictionary<string, object> { ["category"] = "tech" });

        results.Should().HaveCount(1);
    }

    [Fact(Skip = "Integration test – requires valid OpenAI key in appsettings.json")]
    public async Task Integration_StoreAndSearch_WorksWithRealEmbeddingGenerator() {
        // Arrange: create configuration + DI manually
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddOpenAIEmbeddingGenerator(
            modelId: config["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small",
            apiKey: config["OpenAI:ApiKey"]
        );

        services.AddScoped<IVectorDatabaseService, SqliteVectorDatabaseService>();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var vectorDb = scope.ServiceProvider.GetRequiredService<IVectorDatabaseService>();

        // Act: store two docs and search
        await vectorDb.StoreDocumentAsync("test_integration", "doc1", "Machine learning enables AI systems to learn from data.");
        await vectorDb.StoreDocumentAsync("test_integration", "doc2", "Insurance underwriting evaluates risks and pricing.");

        var results = await vectorDb.SearchAsync("test_integration", "how AI learns from examples");

        // Assert
        results.Should().NotBeEmpty();
        results.First().Content.Should().ContainAny("Machine", "AI");
        results.First().RelevanceScore.Should().BeGreaterThan(0.6);
    }
}
