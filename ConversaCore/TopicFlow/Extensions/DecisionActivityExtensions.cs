using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace ConversaCore.TopicFlow.Extensions;

/// <summary>
/// Extension methods for easily adding DecisionActivity to TopicFlow workflows
/// </summary>
public static class DecisionActivityExtensions
{
    /// <summary>
    /// Adds a generic DecisionActivity to the workflow
    /// </summary>
    public static TopicFlow AddDecision<TInput, TEvidence, TResponse>(
        this TopicFlow topicFlow,
        string activityId,
        Kernel kernel,
        ILogger logger,
        TInput input,
        string evidenceContextKey,
        string systemPrompt,
        string userPromptTemplate,
        float temperature = 0.3f,
        string modelId = "gpt-4o",
        bool requireJsonOutput = true)
        where TInput : class
        where TEvidence : class
        where TResponse : class
    {
        var activity = new DecisionActivity<TInput, TEvidence, TResponse>(
            activityId,
            kernel,
            logger,
            input,
            evidenceContextKey,
            systemPrompt,
            userPromptTemplate,
            temperature,
            modelId,
            requireJsonOutput
        );

        topicFlow.Add(activity);
        return topicFlow;
    }

    /// <summary>
    /// Adds a DecisionActivity with fluent configuration
    /// </summary>
    public static TopicFlow AddDecision<TInput, TEvidence, TResponse>(
        this TopicFlow topicFlow,
        string activityId,
        Kernel kernel,
        ILogger logger,
        TInput input,
        string evidenceContextKey,
        Action<DecisionActivityBuilder<TInput, TEvidence, TResponse>> configure)
        where TInput : class
        where TEvidence : class
        where TResponse : class
    {
        var builder = new DecisionActivityBuilder<TInput, TEvidence, TResponse>(
            activityId, kernel, logger, input, evidenceContextKey);
        
        configure(builder);
        
        var activity = builder.Build();
        topicFlow.Add(activity);
        return topicFlow;
    }
}

/// <summary>
/// Builder for fluent configuration of DecisionActivity
/// </summary>
public class DecisionActivityBuilder<TInput, TEvidence, TResponse>
    where TInput : class
    where TEvidence : class
    where TResponse : class
{
    private readonly string _activityId;
    private readonly Kernel _kernel;
    private readonly ILogger _logger;
    private readonly TInput _input;
    private readonly string _evidenceContextKey;
    
    private string _systemPrompt = "You are an AI assistant that analyzes evidence against provided input data and produces structured decisions.";
    private string _userPromptTemplate = "Analyze this evidence: {evidence}";
    private float _temperature = 0.3f;
    private string _modelId = "gpt-4o";
    private bool _requireJsonOutput = true;

    public DecisionActivityBuilder(
        string activityId,
        Kernel kernel,
        ILogger logger,
        TInput input,
        string evidenceContextKey)
    {
        _activityId = activityId;
        _kernel = kernel;
        _logger = logger;
        _input = input;
        _evidenceContextKey = evidenceContextKey;
    }

    public DecisionActivityBuilder<TInput, TEvidence, TResponse> WithSystemPrompt(string systemPrompt)
    {
        _systemPrompt = systemPrompt;
        return this;
    }

    public DecisionActivityBuilder<TInput, TEvidence, TResponse> WithUserPrompt(string userPromptTemplate)
    {
        _userPromptTemplate = userPromptTemplate;
        return this;
    }

    public DecisionActivityBuilder<TInput, TEvidence, TResponse> WithTemperature(float temperature)
    {
        _temperature = temperature;
        return this;
    }

    public DecisionActivityBuilder<TInput, TEvidence, TResponse> WithModel(string modelId)
    {
        _modelId = modelId;
        return this;
    }

    public DecisionActivityBuilder<TInput, TEvidence, TResponse> RequireJsonOutput(bool require = true)
    {
        _requireJsonOutput = require;
        return this;
    }

    public DecisionActivity<TInput, TEvidence, TResponse> Build()
    {
        return new DecisionActivity<TInput, TEvidence, TResponse>(
            _activityId,
            _kernel,
            _logger,
            _input,
            _evidenceContextKey,
            _systemPrompt,
            _userPromptTemplate,
            _temperature,
            _modelId,
            _requireJsonOutput
        );
    }
}