using ConversaCore.Interfaces;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using SimpleBlazorDemo.Services;

namespace SimpleBlazorDemo.Topics.Education;

/// <summary>
/// Helper for constructing a SemanticResponseActivity bound to the
/// life-insurance-basics document collection.
/// </summary>
public static class LifeInsuranceBasicsModule
{
    public const string ActivityId = "LifeInsuranceBasicsSemantic";

    /// <summary>
    /// Creates a semantic response activity that answers strictly from the
    /// life-insurance-basics document collection.
    /// </summary>
    public static SemanticResponseActivity CreateActivity(
        Kernel kernel,
        ILogger logger,
        IVectorDatabaseService? vectorDb)
    {
        var developerPrompt = @"You are an insurance educator focused ONLY on life insurance basics.
Use the provided evidence from the life-insurance-basics document set to answer.
If a user asks about something that is not covered in these documents,
say politely that it is outside the current educational module.
Be concise, friendly, and avoid sales pressure. Aim for 2–4 short paragraphs.";

        var activity = new SemanticResponseActivity(
                id: ActivityId,
                kernel: kernel,
                logger: logger,
                vectorDb: vectorDb,
                collectionName: LifeInsuranceBasicsEmbeddingService.CollectionName)
            .WithDeveloperPrompt(developerPrompt)
            .WithUserPrompt("Please give a concise, beginner-friendly overview of life insurance basics.")
            .WithSkipLLMThreshold(0.92);

        return activity;
    }
}
