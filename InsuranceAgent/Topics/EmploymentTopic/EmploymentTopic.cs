using ConversaCore.Models;
using ConversaCore.TopicFlow;

namespace InsuranceAgent.Topics;

/// <summary>
/// Topic for collecting employment information.
/// Ported from Copilot Studio adaptive card.
/// Event-driven, queue-based flow of activities.
/// </summary>
public class EmploymentTopic : TopicFlow
{
    public const string ActivityId_ShowCard = "ShowEmploymentCard";
    public const string ActivityId_DumpCtx = "DumpCTX";
    public const string ActivityId_Trigger = "TriggerNextTopic";

    /// <summary>
    /// Keywords for topic routing.
    /// </summary>
    public static readonly string[] IntentKeywords = new[] {
        "employment", "job", "work", "career", "occupation", "income", "salary",
        "employed", "unemployed", "retired", "student", "self-employed",
        "full-time", "part-time", "household income", "employment status",
        "profession", "workplace", "employer", "earnings", "wages"
    };

    private readonly ConversaCore.Context.IConversationContext _conversationContext;
    private readonly ILogger<EmploymentTopic> _logger;

    public EmploymentTopic(
        TopicWorkflowContext context,
        ILogger<EmploymentTopic> logger,
        ConversaCore.Context.IConversationContext conversationContext
    ) : base(context, logger, name: "EmploymentTopic")
    {
        _logger = logger;
        _conversationContext = conversationContext;

        Context.SetValue("EmploymentTopic_create", DateTime.UtcNow.ToString("o"));
        Context.SetValue("TopicName", "Employment Information");

        // === Activities in queue order ===
        var showCardActivity = new AdaptiveCardActivity<EmploymentCard, EmploymentModel>(
            ActivityId_ShowCard,
            context,
            cardFactory: card => card.Create(),
            modelContextKey: "EmploymentModel",
            onTransition: (from, to, data) => {
                var stamp = DateTime.UtcNow.ToString("o");
                Console.WriteLine(
                    $"[EmploymentCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
                );
            }
        );

        var isDevelopment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        var triggerActivity = new TriggerTopicActivity(
            ActivityId_Trigger,
            "NextTopicName" // Will be set in context or default to next logical topic
        );

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
        {
            _logger.LogInformation("[{Topic}] Model bound: {ModelType}", Name, e.Model?.GetType().Name);
            
            //// Store employment data in conversation context for other topics to access
            //if (e.Model is EmploymentModel employmentModel)
            //{
            //    Context.SetValue("employment_data", employmentModel);
            //    Context.SetValue("employment_status", employmentModel.EmploymentStatus);
            //    Context.SetValue("household_income_band", employmentModel.HouseholdIncomeBand);
            //    Context.SetValue("occupation", employmentModel.Occupation);
            //    Context.SetValue("estimated_household_income", employmentModel.EstimatedHouseholdIncome);
            //    Context.SetValue("is_employed", employmentModel.IsEmployed);
            //    Context.SetValue("is_full_time_equivalent", employmentModel.IsFullTimeEquivalent);
            //    Context.SetValue("employment_risk_category", employmentModel.EmploymentRiskCategory);
            //    Context.SetValue("can_likely_afford_insurance", employmentModel.CanLikelyAffordInsurance);
            //    Context.SetValue("employment_data_quality_score", employmentModel.EmploymentDataQualityScore);
            //    Context.SetValue("employment_data_quality_grade", employmentModel.EmploymentDataQualityGrade);
            //    Context.SetValue("income_disclosed", employmentModel.IncomeDisclosed);
                
            //    _logger.LogInformation("[{Topic}] Employment - Status: {Status}, Income: {Income}, Risk: {Risk}, Affordability: {Afford}", 
            //        Name, employmentModel.EmploymentStatus, employmentModel.HouseholdIncomeBand, 
            //        employmentModel.EmploymentRiskCategory, employmentModel.CanLikelyAffordInsurance);
            //}
        };

        showCardActivity.ValidationFailed += (s, e) =>
            _logger.LogWarning("[{Topic}] Validation failed: {Message}", Name, e.Exception.Message);

        // === Trigger hook ===
        triggerActivity.TopicTriggered += (sender, e) =>
        {
            _logger.LogInformation("[{Topic}] Triggering next topic: {Next}", Name, e.TopicName);
            _conversationContext.AddTopicToChain(e.TopicName);
        };

        // === Enqueue activities ===
        Add(showCardActivity);
        Add(triggerActivity);
    }

    /// <summary>
    /// Intent detection (keyword matching for employment topics).
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
    public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
    {
        Context.SetValue("EmploymentTopic_runasync", DateTime.UtcNow.ToString("o"));

        var task = base.RunAsync(cancellationToken);

        return task.ContinueWith(t => {
            var result = t.Result;

            var nextTopic = Context.GetValue<string>("NextTopic");
            if (!string.IsNullOrEmpty(nextTopic))
            {
                result.NextTopicName = nextTopic;
                Context.SetValue("NextTopic", null); // reset
            }

            return result;
        });
    }
}