using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Cards;

namespace InsuranceAgent.Topics; 
/// <summary>
/// Demo topic for capturing California residency information.
/// Event-driven, queue-based flow of activities.
/// </summary>
public class CaliforniaResidentDemoTopic : TopicFlow {
    public const string ActivityId_ShowCard = "ShowCaliforniaResidentCard";
    public const string ActivityId_DumpCtx = "DumpCTX";

    /// <summary>
    /// Keywords for topic routing.
    /// </summary>
    public static readonly string[] IntentKeywords = new[] {
        "california resident", "ca resident", "california", "residency"
    };

    private readonly ILogger<CaliforniaResidentDemoTopic> _logger;

    public CaliforniaResidentDemoTopic(
        TopicWorkflowContext context,
        ILogger<CaliforniaResidentDemoTopic> logger
    ) : base(context, logger, name: "CaliforniaResidentDemoTopic") {
        _logger = logger;

        Context.SetValue("CaliforniaResidentDemoTopic_create", DateTime.UtcNow.ToString("o"));
        Context.SetValue("TopicName", "California Resident Demo");

        // === Activities in queue order ===
        var showCardActivity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
            ActivityId_ShowCard,
            context,
            cardFactory: card => card.Create(),
            modelContextKey: "CaliforniaResidentModel",
            onTransition: (from, to, data) => {
                var stamp = DateTime.UtcNow.ToString("o");
                Console.WriteLine(
                    $"[CaliforniaResidentCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
                );
            }
        );

        var isDevelopment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        var dumpCtxActivity = new DumpCtxActivity(ActivityId_DumpCtx, isDevelopment);

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

        showCardActivity.ModelBound += (s, e) =>
            _logger.LogInformation("[{Topic}] Model bound: {ModelType}", Name, e.Model?.GetType().Name);

        showCardActivity.ValidationFailed += (s, e) =>
            _logger.LogWarning("[{Topic}] Validation failed: {Message}", Name, e.Exception.Message);

        // === Enqueue activities ===
        Add(showCardActivity);
        Add(dumpCtxActivity);
    }


    /// <summary>
    /// Intent detection (basic keyword matching).
    /// </summary>
    public override async Task<float> CanHandleAsync(
        string message,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(message)) return 0f;
        var msg = message.ToLowerInvariant();

        foreach (var kw in IntentKeywords) {
            if (msg.Contains(kw)) return 1.0f;
        }
        return 0f;
    }

    /// <summary>
    /// Execute the topic’s activities in queue order.
    /// </summary>
    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
        Context.SetValue("CaliforniaResidentDemoTopic_runasync", DateTime.UtcNow.ToString("o"));

        var task = base.RunAsync(cancellationToken);

        return task.ContinueWith(t => {
            var result = t.Result;

            var nextTopic = Context.GetValue<string>("NextTopic");
            if (!string.IsNullOrEmpty(nextTopic)) {
                result.NextTopicName = nextTopic;
                Context.SetValue("NextTopic", null); // reset
            }

            return result;
        });
    }
}
