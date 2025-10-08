using ConversaCore.Models;
using ConversaCore.Topics;

namespace ConversaCore.SystemTopics;

using System;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;


public sealed class MultipleTopicsMatchedTopic : TopicFlow
{
    public const string ClarifyActivityId = "clarify";

    public MultipleTopicsMatchedTopic(TopicWorkflowContext context, ILogger<MultipleTopicsMatchedTopic> logger)
        : base(context, logger)
    {
        Context.SetValue("MultipleTopicsMatchedTopic_create", DateTime.UtcNow.ToString("o"));
        Add(new MultipleTopicsMatchedActivity(ClarifyActivityId, "🤔 I found multiple possible intents. Could you clarify what you need?"));
        //StartAt(ClarifyActivityId);
    }

    public override string Name => "MultipleTopicsMatched";
    public override int Priority => 70;

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
        => Task.FromResult(0.0f); // normally invoked explicitly by router
}
