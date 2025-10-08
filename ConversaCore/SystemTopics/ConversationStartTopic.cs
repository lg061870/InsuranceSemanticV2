using System;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;

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

    public ConversationStartTopic(TopicWorkflowContext context, ILogger<ConversationStartTopic> logger)
        : base(context, logger) {

        Context.SetValue("ConversationStartTopic_create", DateTime.UtcNow.ToString("o"));

        // Add greeting as the first activity (entry point)
        var greet = new GreetingActivity(GreetActivityId);
        Add(greet);

        logger.LogInformation("[ConversationStartTopic] Added GreetingActivity '{ActivityId}' to queue", GreetActivityId);

        // Mark this topic as initialized
        Context.SetValue(ContextFlag, true);
    }

    public override string Name => "ConversationStart";
    public override int Priority => int.MaxValue;

    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
        Context.SetValue("ConversationStartTopic_runasync", DateTime.UtcNow.ToString("o"));
        base.Context.SetValue("TopicName", "Conversation Start");
        return base.RunAsync(cancellationToken);
    }

    public override async Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
        // Only match the special initialization intent and only if not already run
        var hasRun = Context.GetValue<bool>(ContextFlag, false);
        return (message == InitIntent && !hasRun) ? 1.0f : 0.0f;
    }

    public override async Task<ConversaCore.Models.TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) {
        // Mark as run so it cannot be triggered again
        
        Context.SetValue(ContextFlag, true);
        return await RunAsync(cancellationToken);
    }
}
