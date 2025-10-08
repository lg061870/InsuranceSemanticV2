using ConversaCore.Models;
using ConversaCore.Topics;

namespace ConversaCore.SystemTopics;

using System;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;


public sealed class OnErrorTopic : TopicFlow
{
    public const string ErrorActivityId = "error";

    public OnErrorTopic(TopicWorkflowContext context, ILogger<OnErrorTopic> logger)
        : base(context, logger)
    {
        Context.SetValue("OnErrorTopic_create", DateTime.UtcNow.ToString("o"));
        Add(new OnErrorActivity(ErrorActivityId, "⚠️ Sorry, something went wrong. Let’s try that again."));
        //StartAt(ErrorActivityId);
    }

    public override string Name => "OnError";
    public override int Priority => int.MinValue + 10;

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
        => Task.FromResult(0.0f); // typically invoked explicitly by your orchestration
}
