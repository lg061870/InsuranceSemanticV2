using System;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Context;
using ConversaCore.Cards;

namespace ConversaCore.SystemTopics;

using ConversaCore.Models;

/// <summary>
/// Kicks off the conversation with a friendly welcome. Highest priority.
/// </summary>
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;


public sealed class ConversationStartTopic : TopicFlow {
    public const string GreetActivityId = "greet";
    private const string InitIntent = "__init__";
    private const string ContextFlag = "ConversationStartTopic.HasRun";
    
    private readonly IConversationContext _conversationContext;

    public ConversationStartTopic(
        TopicWorkflowContext context, 
        ILogger<ConversationStartTopic> logger,
        IConversationContext conversationContext)
        : base(context, logger) {
        
        _conversationContext = conversationContext;

        // Initialize generic conversation metadata
        _conversationContext.SetValue("ConversationStartTopic_create", DateTime.UtcNow.ToString("o"));
        _conversationContext.SetValue("ConversationId", Guid.NewGuid().ToString());
        _conversationContext.SetValue("ConversationStartTime", DateTime.UtcNow);
        
        // Initialize basic global variable structure
        InitializeGenericGlobalStructure();

        // Add greeting as the first activity (entry point)
        var greet = new GreetingActivity(GreetActivityId);
        Add(greet);

        // Add TCPA/CCPA compliance collection immediately after greeting
        // ComplianceTopic handles the California resident check internally
        var complianceActivity = new TriggerTopicActivity("CollectCompliance", "ComplianceTopic", waitForCompletion: true);
        Add(complianceActivity);

        logger.LogInformation("[ConversationStartTopic] Added GreetingActivity '{ActivityId}' to queue", GreetActivityId);
        logger.LogInformation("[ConversationStartTopic] Added ComplianceTopic trigger for TCPA/CCPA collection");
        logger.LogInformation("[ConversationStartTopic] Initialized generic global structure");

        // Mark this topic as initialized
        Context.SetValue(ContextFlag, true);
    }

    /// <summary>
    /// Initializes the generic global variable structure.
    /// Domain-specific initialization should be handled by the consuming application.
    /// Follows Copilot Studio pattern of declaring global variables upfront.
    /// </summary>
    private void InitializeGenericGlobalStructure()
    {
        // Initialize basic conversation-level metadata
        _conversationContext.SetValue("Global_ConversationActive", true);
        _conversationContext.SetValue("Global_TopicHistory", new List<string>());
        _conversationContext.SetValue("Global_UserInteractionCount", 0);
        
        // Note: Domain-specific models and variables should be initialized
        // by the consuming application (e.g., HybridChatService for insurance domain)
        
        _conversationContext.SetValue("Global_GenericStructureInitialized", true);
    }

    public override string Name => "ConversationStart";
    public override int Priority => int.MaxValue;

    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
        _conversationContext.SetValue("ConversationStartTopic_runasync", DateTime.UtcNow.ToString("o"));
        _conversationContext.SetValue("TopicName", "Conversation Start");
        _conversationContext.SetCurrentTopic(Name);
        return base.RunAsync(cancellationToken);
    }

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
        // Only match the special initialization intent and only if not already run
        var hasRun = Context.GetValue<bool>(ContextFlag, false);
        return Task.FromResult((message == InitIntent && !hasRun) ? 1.0f : 0.0f);
    }

    public override async Task<ConversaCore.Models.TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) {
        // Mark as run so it cannot be triggered again
        _conversationContext.SetValue(ContextFlag, true);
        _conversationContext.AddTopicToHistory(Name);
        return await RunAsync(cancellationToken);
    }
}
