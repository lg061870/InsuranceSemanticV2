using ConversaCore.Interfaces;
using ConversaCore.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Services;

/// <summary>
/// Insurance-specific document embedding service.
/// Inherits base functionality but can be customized for insurance document paths.
/// </summary>
public class InsuranceDocumentEmbeddingService : ConversaCore.Services.DocumentEmbeddingService {
    public InsuranceDocumentEmbeddingService(
        IVectorDatabaseService vectorDb,
        IDocumentProcessingService docProcessor,
        IConfiguration configuration,
        ILogger<InsuranceDocumentEmbeddingService> logger)
        : base(vectorDb, docProcessor, configuration, logger) {
    }

    // Can override methods here for insurance-specific behavior if needed
    // For now, the base implementation is sufficient
}
