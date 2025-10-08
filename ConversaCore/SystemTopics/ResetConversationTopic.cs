using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.Topics;

namespace ConversaCore.SystemTopics;

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;


public sealed class ResetConversationTopic : TopicFlow
{
    public const string ResetActivityId = "reset";
    private static readonly string[] _cues = new[]
    {
        "reset", "start over", "restart", "clear chat", "new conversation"
    };

    public ResetConversationTopic(TopicWorkflowContext context, ILogger<ResetConversationTopic> logger)
        : base(context, logger)
    {
        Context.SetValue("ResetConversationTopic_create", DateTime.UtcNow.ToString("o"));
        Add(new ResetActivity(ResetActivityId, "🔄 Conversation has been reset. How would you like to begin?"));
        //StartAt(ResetActivityId);
    }

    public override string Name => "ResetConversation";
    public override int Priority => 95;

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
    {
        var m = message?.ToLowerInvariant() ?? string.Empty;
        return Task.FromResult(_cues.Any(m.Contains) ? 0.9f : 0.0f);
    }
}
