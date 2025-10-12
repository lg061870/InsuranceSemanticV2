using ConversaCore.Models;
using ConversaCore.Topics;

namespace ConversaCore.SystemTopics;

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;


public sealed class EscalateTopic : TopicFlow
{
    public const string EscalateActivityId = "escalate";
    private static readonly string[] _cues = new[]
    {
        "human", "agent", "representative", "operator", "escalate", "speak to a person", "talk to a person"
    };

    public EscalateTopic(TopicWorkflowContext context, ILogger<EscalateTopic> logger)
        : base(context, logger)
    {
        Context.SetValue("EscalateTopic_create", DateTime.UtcNow.ToString("o"));
        Add(new EscalateActivity(EscalateActivityId, "☎️ I’ll connect you with a human agent now."));
        //StartAt(EscalateActivityId);
    }

    public override string Name => "Escalate";
    public override int Priority => 50;

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
    {
        var m = message?.ToLowerInvariant() ?? string.Empty;
        return Task.FromResult(_cues.Any(m.Contains) ? 0.9f : 0.0f);
    }
        public override void Reset()
        {
            ClearActivities();
            Add(new EscalateActivity(EscalateActivityId, "☎️ I’ll connect you with a human agent now."));
        }
}
