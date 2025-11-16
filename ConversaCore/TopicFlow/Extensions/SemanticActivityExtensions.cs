using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace ConversaCore.TopicFlow.Extensions;

/// <summary>
/// Fluent API extensions for adding semantic activities to topic flows.
/// Provides convenient methods for integrating AI-powered activities into ConversaCore workflows.
/// </summary>
public static class SemanticActivityExtensions
{
    /// <summary>
    /// Add a custom semantic activity to the topic flow.
    /// </summary>
    public static TopicFlow AddSemantic<T>(
        this TopicFlow topic, 
        T activity) 
        where T : SemanticActivity
    {
        topic.Add(activity);
        return topic;
    }

    /// <summary>
    /// Add an insurance decision activity that analyzes user profiles against JSON rules.
    /// </summary>
    public static TopicFlow AddInsuranceDecision(
        this TopicFlow topic,
        string activityId,
        Kernel kernel,
        ILogger logger,
        string rulesFilePath,
        string userProfileContextKey = "UserProfile",
        float temperature = 0.3f) // Lower temp for more deterministic results
    {
        var activity = new InsuranceDecisionActivity(activityId, kernel, logger)
        {
            RulesFilePath = rulesFilePath,
            UserProfileContextKey = userProfileContextKey,
            Temperature = temperature,
            RequireJsonOutput = true
        };
        
        topic.Add(activity);
        return topic;
    }

    /// <summary>
    /// Add a semantic response activity for contextual conversation redirection.
    /// </summary>
    public static TopicFlow AddSemanticResponse(
        this TopicFlow topic,
        string activityId,
        Kernel kernel,
        ILogger logger,
        string developerInstruction,
        float temperature = 0.8f) // Higher temp for more creative responses
    {
        var activity = new SemanticResponseActivity(activityId, kernel, logger)
            .WithDeveloperPrompt(developerInstruction);

        activity.Temperature = temperature;

        topic.Add(activity);
        return topic;
    }


    /// <summary>
    /// Add a semantic response activity with fluent configuration.
    /// </summary>
    public static TopicFlow AddSemanticResponse(
        this TopicFlow topic,
        string activityId,
        Kernel kernel,
        ILogger logger,
        Action<SemanticResponseActivity> configure)
    {
        var activity = new SemanticResponseActivity(activityId, kernel, logger);
        configure(activity);
        
        topic.Add(activity);
        return topic;
    }

    /// <summary>
    /// Add a prompt activity with custom system and user prompts.
    /// </summary>
    public static TopicFlow AddPrompt(
        this TopicFlow topic,
        string activityId,
        Kernel kernel,
        ILogger logger,
        string systemPrompt,
        string userPromptTemplate,
        bool requireJsonOutput = false,
        float temperature = 0.7f)
    {
        var activity = new PromptActivity(activityId, kernel, logger)
        {
            SystemPrompt = systemPrompt,
            UserPromptTemplate = userPromptTemplate,
            RequireJsonOutput = requireJsonOutput,
            Temperature = temperature
        };
        
        topic.Add(activity);
        return topic;
    }

    /// <summary>
    /// Add a document analysis activity that uses vector search for context.
    /// </summary>
    public static TopicFlow AddDocumentAnalysis(
        this TopicFlow topic,
        string activityId,
        Kernel kernel,
        ILogger logger,
        string analysisPrompt,
        int maxDocuments = 5,
        float temperature = 0.5f)
    {
        // TODO: Implement DocumentAnalysisActivity when vector search integration is ready
        throw new NotImplementedException("DocumentAnalysisActivity will be implemented in the next phase");
    }

    public static TopicFlow AddSemanticResponse(
        this TopicFlow topic,
        string activityId,
        Kernel kernel,
        ILogger logger,
        string developerPrompt,
        string userPrompt,
        float temperature = 0.8f) {
        var activity = new SemanticResponseActivity(activityId, kernel, logger)
            .WithDeveloperPrompt(developerPrompt)
            .WithUserPrompt(userPrompt);

        activity.Temperature = temperature;

        topic.Add(activity);
        return topic;
    }

}