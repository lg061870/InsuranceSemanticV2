using ConversaCore.Models;
using ConversaCore.TopicFlow;

namespace InsuranceAgent.Topics;

/// <summary>
/// Topic for collecting lead management and sales tracking information.
/// Ported from Copilot Studio adaptive card.
/// Event-driven, queue-based flow of activities.
/// </summary>
public class LeadDetailsTopic : TopicFlow
{
    public const string ActivityId_ShowCard = "ShowLeadDetailsCard";
    public const string ActivityId_DumpCtx = "DumpCTX";
    public const string ActivityId_Trigger = "TriggerNextTopic";

    /// <summary>
    /// Keywords for topic routing.
    /// </summary>
    public static readonly string[] IntentKeywords = new[] {
        "lead details", "lead name", "lead source", "interest level", "lead intent",
        "appointment", "follow up", "sales agent", "notes", "lead url",
        "language", "preferred language", "referral", "website", "phone",
        "high interest", "medium interest", "low interest", "buy", "learn", "compare",
        "schedule", "callback", "urgent", "priority", "lead management"
    };

    private readonly ConversaCore.Context.IConversationContext _conversationContext;
    private readonly ILogger<LeadDetailsTopic> _logger;

    public LeadDetailsTopic(
        TopicWorkflowContext context,
        ILogger<LeadDetailsTopic> logger,
        ConversaCore.Context.IConversationContext conversationContext
    ) : base(context, logger, name: "LeadDetailsTopic")
    {
        _logger = logger;
        _conversationContext = conversationContext;

        Context.SetValue("LeadDetailsTopic_create", DateTime.UtcNow.ToString("o"));
        Context.SetValue("TopicName", "Lead Details");

        // === Activities in queue order ===
        var showCardActivity = new AdaptiveCardActivity<LeadDetailsCard, LeadDetailsModel>(
            ActivityId_ShowCard,
            context,
            cardFactory: card => card.Create(),
            modelContextKey: "LeadDetailsModel",
            onTransition: (from, to, data) => {
                var stamp = DateTime.UtcNow.ToString("o");
                Console.WriteLine(
                    $"[LeadDetailsCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
                );
            }
        );

        var isDevelopment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";


        // === Event hooks for AdaptiveCard lifecycle ===
        showCardActivity.CardJsonEmitted += (s, e) =>
            _logger.LogInformation("[{Topic}] Card JSON emitted (mode={Mode})", Name, e.RenderMode);

        showCardActivity.CardJsonSending += (s, e) =>
            _logger.LogInformation("[{Topic}] Card JSON sending (mode={Mode})", Name, e.RenderMode);

        showCardActivity.CardJsonSent += (s, e) =>
            _logger.LogInformation("[{Topic}] Card JSON sent (mode={Mode})", Name, e.RenderMode);

        showCardActivity.CardJsonRendered += (s, e) =>
            _logger.LogInformation("[{Topic}] Card JSON rendered on client at {Time}", Name, e.RenderedAt);

        showCardActivity.CardDataReceived += (s, e) =>
            _logger.LogInformation("[{Topic}] Card data received: {Keys}", Name, string.Join(",", e.Data.Keys));

        showCardActivity.ModelBound += (s, e) => _logger.LogInformation("[{Topic}] Model bound: {ModelType}", Name, e.Model?.GetType().Name);

        showCardActivity.ValidationFailed += (s, e) =>
            _logger.LogWarning("[{Topic}] Validation failed: {Message}", Name, e.Exception.Message);

        // === Enqueue activities ===
        Add(showCardActivity);
    }

    /// <summary>
    /// Intent detection (keyword matching for lead management topics).
    /// </summary>
    public override Task<float> CanHandleAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message)) return Task.FromResult(0f);
        var msg = message.ToLowerInvariant();

        var matchCount = 0;
        foreach (var kw in IntentKeywords)
        {
            if (msg.Contains(kw))
            {
                matchCount++;
            }
        }

        // Calculate confidence based on keyword matches
        var confidence = matchCount > 0 ? Math.Min(1.0f, matchCount / 3.0f) : 0f;
        
        _logger.LogDebug("[{Topic}] Intent confidence: {Confidence} for message: {Message}", 
            Name, confidence, message);
            
        return Task.FromResult(confidence);
    }

    /// <summary>
    /// Execute the topic's activities in queue order.
    /// Also handles optional NextTopic context handoff.
    /// </summary>
    public override async Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
    {
        Context.SetValue("LeadDetailsTopic_runasync", DateTime.UtcNow.ToString("o"));

        var result = await base.RunAsync(cancellationToken);

        var nextTopic = Context.GetValue<string>("NextTopic");
        if (!string.IsNullOrEmpty(nextTopic))
        {
            result.NextTopicName = nextTopic;
            Context.SetValue("NextTopic", null); // reset
        }

        return result;
    }
}
