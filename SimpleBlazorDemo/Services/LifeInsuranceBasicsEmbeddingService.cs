using ConversaCore.Interfaces;
using Microsoft.Extensions.Logging;

namespace SimpleBlazorDemo.Services;

/// <summary>
/// Module-specific ingestion service for the "life-insurance-basics" educational topic.
/// Reads documents from a dedicated folder under the shared documents path
/// and indexes them into a dedicated vector collection.
/// </summary>
public class LifeInsuranceBasicsEmbeddingService
{
    public const string CollectionName = "life_insurance_basics";

    private readonly IDocumentEmbeddingService _embeddingService;
    private readonly IDocumentProcessingService _docProcessor;
    private readonly IVectorDatabaseService _vectorDb;
    private readonly ILogger<LifeInsuranceBasicsEmbeddingService> _logger;
    private readonly string _moduleDirectory;

    public LifeInsuranceBasicsEmbeddingService(
        IDocumentEmbeddingService embeddingService,
        IDocumentProcessingService docProcessor,
        IVectorDatabaseService vectorDb,
        ILogger<LifeInsuranceBasicsEmbeddingService> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _docProcessor = docProcessor ?? throw new ArgumentNullException(nameof(docProcessor));
        _vectorDb = vectorDb ?? throw new ArgumentNullException(nameof(vectorDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _moduleDirectory = Path.Combine(_embeddingService.DocumentsPath, "life-insurance-basics");
        if (!Directory.Exists(_moduleDirectory))
        {
            Directory.CreateDirectory(_moduleDirectory);
            _logger.LogInformation("Created life-insurance-basics documents directory at {Dir}", _moduleDirectory);
        }
    }

    /// <summary>
    /// Physical folder where life-insurance-basics documents should be dropped.
    /// </summary>
    public string ModuleDirectory => _moduleDirectory;

    /// <summary>
    /// Processes all documents for this module and indexes them into the
    /// dedicated life_insurance_basics vector collection.
    /// </summary>
    public async Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Syncing life-insurance-basics documents from {Dir} into collection {Collection}",
            _moduleDirectory, CollectionName);

        var chunks = await _docProcessor.ProcessDirectoryAsync(
            _moduleDirectory,
            filePattern: "*.*",
            cancellationToken: cancellationToken);

        if (chunks.Count == 0)
        {
            _logger.LogInformation("No processable documents found for life-insurance-basics in {Dir}", _moduleDirectory);
            return 0;
        }

        var stored = await _vectorDb.StoreBatchAsync(CollectionName, chunks, cancellationToken);
        _logger.LogInformation(
            "Indexed {Stored}/{Total} chunks for life-insurance-basics into collection {Collection}",
            stored, chunks.Count, CollectionName);

        return stored;
    }
}
