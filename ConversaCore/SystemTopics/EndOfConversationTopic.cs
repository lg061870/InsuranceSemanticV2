using ConversaCore.Models;
using ConversaCore.Topics;

namespace ConversaCore.SystemTopics;

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;


public sealed class EndOfConversationTopic : TopicFlow
{
    public const string EndActivityId = "end";
    private static readonly string[] _cues = new[]
    {
        "bye", "goodbye", "thanks, bye", "end chat", "finish", "that’s all", "that is all", "you can stop"
    };

    public EndOfConversationTopic(TopicWorkflowContext context, ILogger<EndOfConversationTopic> logger)
        : base(context, logger)
    {
        Context.SetValue("EndOfConversationTopic_create", DateTime.UtcNow.ToString("o"));
        Add(new EndActivity(EndActivityId, "👋 Thanks for chatting. Have a great day!"));
        //StartAt(EndActivityId);
    }

    public override string Name => "EndOfConversation";
    public override int Priority => 90;

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
    {
        var m = message?.ToLowerInvariant() ?? string.Empty;
        return Task.FromResult(_cues.Any(m.Contains) ? 0.85f : 0.0f);
    }
        public override void Reset()
        {
            ClearActivities();
            Add(new EndActivity(EndActivityId, "👋 Thanks for chatting. Have a great day!"));
        }
}
