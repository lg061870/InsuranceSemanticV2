using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Cards;
using InsuranceAgent.Topics.CaliforniaResidentTopic;
using ConversaCore.Context;

namespace InsuranceAgent.Topics; 
/// <summary>
/// Demo topic for capturing California residency information.
/// Event-driven, queue-based flow of activities.
/// </summary>
public class CaliforniaResidentDemoTopic : TopicFlow {
    public const string ActivityId_ShowCard = "ShowCaliforniaResidentCard";
    public const string ActivityId_TriggerConsole = "TriggerCustomerConsole";
    public const string ActivityId_TriggerNext = "TriggerNextTopic";

    /// <summary>
    /// Keywords for topic routing.
    /// </summary>
    public static readonly string[] IntentKeywords = new[] {
        "california resident", "ca resident", "california", "residency"
    };

    private readonly ILogger<CaliforniaResidentDemoTopic> _logger;
    private readonly IConversationContext? _conversationContext;
    private static int _constructorCallCount = 0;

    public CaliforniaResidentDemoTopic(
        TopicWorkflowContext context,
        ILogger<CaliforniaResidentDemoTopic> logger,
        IConversationContext? conversationContext = null
    ) : base(context, logger, name: "CaliforniaResidentDemoTopic") {
        _logger = logger;
        _conversationContext = conversationContext;
        
        _logger.LogWarning("[DEBUG] CaliforniaResidentDemoTopic CONSTRUCTOR #{Count} called at {Time}", 
            ++_constructorCallCount, DateTime.UtcNow);

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
        
        // Add EventTriggerActivity to show customer console after card is displayed
        var triggerConsoleActivity = EventTriggerActivity.CreateFireAndForget(
            ActivityId_TriggerConsole,
            "ui.customer-console.show",
            new {
                trigger = "california-resident-card",
                timestamp = DateTime.UtcNow,
                context = new {
                    domain = "insurance",
                    flowType = "california-residency",
                    stage = "post-card-display"
                },
                animation = new {
                    type = "slide-in",
                    direction = "right",
                    duration = 300
                }
            },
            _logger,
            _conversationContext
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

        showCardActivity.ModelBound += (s, e) => {
            _logger.LogInformation("[{Topic}] Model bound: {ModelType}", Name, e.Model?.GetType().Name);
            
            // Store California residency data in context for decision making
            if (e.Model is CaliforniaResidentModel model) {
                Context.SetValue("is_california_resident", model.IsCaliforniaResident);
                Context.SetValue("california_zip_code", model.ZipCode);
                
                _logger.LogInformation("[{Topic}] Stored CA residency: {IsCA}, ZIP: {Zip}", 
                    Name, model.IsCaliforniaResident, model.ZipCode);
            }
        };

        showCardActivity.ValidationFailed += (s, e) =>
            _logger.LogWarning("[{Topic}] Validation failed: {Message}", Name, e.Exception.Message);

        // Add TriggerTopicActivity to continue to MarketingTypeOneTopic after collecting residency info
        var triggerNextActivity = new TriggerTopicActivity(
            ActivityId_TriggerNext,
            "MarketingTypeOneTopic", // Default next topic - could be made configurable
            _logger,
            waitForCompletion: false,
            _conversationContext
        );

        triggerNextActivity.TopicTriggered += (sender, e) => {
            _logger.LogInformation("[{Topic}] Triggering next topic: {Next}", Name, e.TopicName);
            if (_conversationContext != null) {
                _conversationContext.AddTopicToChain(e.TopicName);
            }
        };

        // === Enqueue activities ===
        Add(showCardActivity);
        Add(triggerConsoleActivity); // Show console after card is displayed  
        Add(triggerNextActivity); // Continue to next topic after collecting residency info
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
