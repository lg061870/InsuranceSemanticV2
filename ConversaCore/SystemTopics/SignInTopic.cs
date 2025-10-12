using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Models;
using ConversaCore.Topics;

namespace ConversaCore.SystemTopics; 

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;


public sealed class SignInTopic : TopicFlow
{
    public const string SignInActivityId = "signin";
    private static readonly string[] _cues = new[]
    {
        "sign in", "login", "log in", "authenticate", "authorization", "verify identity"
    };

    public SignInTopic(TopicWorkflowContext context, ILogger<SignInTopic> logger)
        : base(context, logger)
    {
        Context.SetValue("SignInTopic_create", DateTime.UtcNow.ToString("o"));
        Add(new SignInActivity(SignInActivityId, "🔐 Please sign in to continue."));
        //StartAt(SignInActivityId);
    }

    public override string Name => "SignIn";
    public override int Priority => 80;

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
    {
        var m = message?.ToLowerInvariant() ?? string.Empty;
        return Task.FromResult(_cues.Any(m.Contains) ? 0.8f : 0.0f);
    }
        public override void Reset()
        {
            ClearActivities();
            Add(new SignInActivity(SignInActivityId, "🔐 Please sign in to continue."));
        }
}
