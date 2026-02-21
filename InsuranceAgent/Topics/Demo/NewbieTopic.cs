using ConversaCore.Context;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Topics.Demo;

/// <summary>
/// Minimal demo topic for 'newbie' / first-time visitors.
/// Keeps the flow intentionally small for demo purposes.
/// </summary>
public class NewbieTopic : TopicFlow
{
    private readonly ILogger<NewbieTopic> _logger;

    public NewbieTopic(
        TopicWorkflowContext context,
        ILogger<NewbieTopic> logger,
        IConversationContext conversationContext) : base(context, logger, "NewbieTopic")
    {
        _logger = logger;
        BuildWorkflow();
    }

    private void BuildWorkflow()
    {
        Add(new SimpleActivity("Welcome", "👋 Hi — welcome! I can help with quick insurance questions or show you a short guide."));
        Add(new SimpleActivity("OfferHelp", "Would you like a quick explanation of term life vs whole life, or get a fast estimate?"));
    }

    public override Task<float> CanHandleAsync(string input, CancellationToken cancellationToken = default)
    {
        var keywords = new[] { "hi", "hello", "new here", "first time", "just browsing", "what is", "insurance" };
        return Task.FromResult(keywords.Any(k => input?.Contains(k, StringComparison.OrdinalIgnoreCase) == true) ? 0.6f : 0.0f);
    }

    public static readonly string[] IntentKeywords = new[]
    {
        "newbie",
        "first time",
        "just browsing",
        "what is insurance",
        "explain insurance"
    };
}
