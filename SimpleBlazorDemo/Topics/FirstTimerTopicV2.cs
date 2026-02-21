using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Context;
using ConversaCore.Interfaces;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;
using SimpleBlazorDemo.Cards;
using SimpleBlazorDemo.Models;

namespace SimpleBlazorDemo.Topics;

/// <summary>
/// Initial presentation topic for first-time visitors.
/// </summary>
public class InitialpresentationTopic : TopicFlow
{
    private readonly ILogger<InitialpresentationTopic> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConversationContext _conversationContext;

    public InitialpresentationTopic(
        TopicWorkflowContext context,
        ILogger<InitialpresentationTopic> logger,
        IConversationContext conversationContext,
        ILoggerFactory loggerFactory)
        : base(context, logger, "InitialpresentationTopic")
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _conversationContext = conversationContext;
        BuildWorkflow();
    }

    public override void Reset()
    {
        base.Reset();
        BuildWorkflow();
    }

    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[InitialpresentationTopic] RunAsync starting (State={State})", State);
        return base.RunAsync(cancellationToken);
    }

    private void BuildWorkflow()
    {
        // 1) Welcome and introduction
        Add(new SimpleActivity(
            "Welcome",
            "👋 Hi, I’m your virtual insurance guide. I’m here to help you make sense of insurance, at your own pace and in plain language."
        ));

        // Short pause so the welcome can be read
        Add(DelayActivity.Create(
            "WelcomePause",
            TimeSpan.FromSeconds(1)
        ));

        // 2) Open, unbiased question about their needs
        Add(new SimpleActivity(
            "OpenHelpQuestion",
            "To start, how can I help you today in your search for insurance? You can describe what you’re looking for in your own words."
        ));

        // Another brief pause before showing the name/ZIP card
        Add(DelayActivity.Create(
            "PreNameCapturePause",
            TimeSpan.FromSeconds(1)
        ));

        // 3) Ask for their name (and optional ZIP) via a simple adaptive card
        Add(new AdaptiveCardActivity<FirstTimerContactCard, FirstTimerContactModel>(
            "FirstNameCapture",
            Context,
            card => card.Create(name: string.Empty, zip: string.Empty)
        ));

        // 4) Short pause after contact details
        Add(DelayActivity.Create(
            "PostContactPause",
            TimeSpan.FromSeconds(1)
        ));

        // 5) Request TCPA consent for all users, with California-specific
        //    privacy copy shown on the same card when relevant.
        Add(new AdaptiveCardActivity<FirstTimerConsentCard, FirstTimerConsentModel>(
            "FirstTimerConsent",
            Context,
            card => card.Create()
        ));

        // Short pause after consent before asking what they need
        Add(DelayActivity.Create(
            "PostConsentPause",
            TimeSpan.FromSeconds(1)
        ));

        // 6) Ask what they are looking for in insurance (quick answer buttons)
        Add(new QuickAnswerActivity(
            "InsuranceNeedsQuestion",
            "Now, tell me what you’re looking for in insurance?",
            new[]
            {
                "I’m new and want the basics",
                "Help me estimate how much coverage I need",
                "Compare term vs whole life",
                "I’m looking for a specific quote or policy"
            },
            Context,
            _loggerFactory.CreateLogger<QuickAnswerActivity>()
        ));

        // 7) Route based on what they selected. For now we
        // focus on "I’m new and want the basics" and send
        // them to InsuranceBasicsTopic as a warm-up topic.
        // We currently hand off control instead of resuming
        // this topic after the basics.
        Add(ConditionalActivity<TriggerTopicActivity>.Switch(
            "RouteAfterInsuranceNeeds",
            ctx => {
                var model = Context.GetValue<Dictionary<string, object>>("InsuranceNeedsQuestion");
                if (model != null && model.TryGetValue("answer", out var raw))
                {
                    switch (raw)
                    {
                        case string s:
                            return s;
#if NET
                        case System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String:
                            return je.GetString() ?? string.Empty;
#endif
                        default:
                            return raw.ToString() ?? string.Empty;
                    }
                }

                return string.Empty;
            },
            new Dictionary<string, Func<string, TopicWorkflowContext, TriggerTopicActivity>>
            {
                ["I’m new and want the basics"] = (id, ctx) => new TriggerTopicActivity(
                    id,
                    "InsuranceBasicsTopic",
                    _loggerFactory.CreateLogger<TriggerTopicActivity>()
                )
            },
            defaultBranch: null,
            logger: _loggerFactory.CreateLogger<ConditionalActivity<TriggerTopicActivity>>()
        ));
    }

    public override Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default)
    {
        // High score for a dedicated test phrase, so it wins routing
        // when you click the "First Timer V2" prompt suggestion.
        if (!string.IsNullOrWhiteSpace(input) &&
            input.Contains("first timer v2", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(0.95f);
        }

        return Task.FromResult(0.0f);
    }
}
