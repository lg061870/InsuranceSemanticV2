using ConversaCore.Models;
using ConversaCore.Topics;

namespace ConversaCore.SystemTopics;

using System;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;


public sealed class FallbackTopic : TopicFlow
{
    public const string FallbackActivityId = "fallback";

    public FallbackTopic(TopicWorkflowContext context, ILogger<FallbackTopic> logger)
        : base(context, logger)
    {
        Context.SetValue("FallbackTopic_create", DateTime.UtcNow.ToString("o"));
        Add(new FallbackActivity(FallbackActivityId, "❓ I’m not sure I understood. Could you rephrase?"));
        //StartAt(FallbackActivityId);
    }

    public override string Name => "Fallback";
    public override int Priority => int.MinValue; // last resort

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
        => Task.FromResult(0.01f); // keep it available but always last
}
