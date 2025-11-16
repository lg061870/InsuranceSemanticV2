using ConversaCore.Models;
using ConversaCore.TopicFlow;

namespace InsuranceAgent.Topics;

/// <summary>
/// Topic for collecting health information.
/// Ported from Copilot Studio adaptive card.
/// Event-driven, queue-based flow of activities.
/// </summary>
public class HealthInfoTopic : TopicFlow
{
    public const string ActivityId_ShowCard = "ShowHealthInfoCard";
    public const string ActivityId_DumpCtx = "DumpCTX";
    public const string ActivityId_Trigger = "TriggerNextTopic";

    /// <summary>
    /// Keywords for topic routing.
    /// </summary>
    public static readonly string[] IntentKeywords = new[] {
        "health", "medical", "tobacco", "smoking", "smoker", "health insurance",
        "medical conditions", "diabetes", "heart condition", "blood pressure",
        "height", "weight", "bmi", "health info", "medical history",
        "medication", "doctor", "hospital", "illness", "disease", "condition"
    };

    private readonly ConversaCore.Context.IConversationContext _conversationContext;
    private readonly ILogger<HealthInfoTopic> _logger;

    public HealthInfoTopic(
        TopicWorkflowContext context,
        ILogger<HealthInfoTopic> logger,
        ConversaCore.Context.IConversationContext conversationContext
    ) : base(context, logger, name: "HealthInfoTopic")
    {
        _logger = logger;
        _conversationContext = conversationContext;

        Context.SetValue("HealthInfoTopic_create", DateTime.UtcNow.ToString("o"));
        Context.SetValue("TopicName", "Health Information");

        // === Activities in queue order ===
        var showCardActivity = new AdaptiveCardActivity<HealthInfoCard, HealthInfoModel>(
            ActivityId_ShowCard,
            context,
            cardFactory: card => card.Create(),
            modelContextKey: "HealthInfoModel",
            onTransition: (from, to, data) => {
                var stamp = DateTime.UtcNow.ToString("o");
                Console.WriteLine(
                    $"[HealthInfoCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
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
            
            // Store health data in conversation context for other topics to access
            if (e.Model is HealthInfoModel healthInfoModel)
            {
                Context.SetValue("health_info_data", healthInfoModel);
                Context.SetValue("uses_tobacco", healthInfoModel.UsesTobacco);
                Context.SetValue("selected_medical_conditions", healthInfoModel.SelectedMedicalConditions);
                Context.SetValue("has_health_insurance", healthInfoModel.HasHealthInsurance);
                Context.SetValue("height", healthInfoModel.Height);
                Context.SetValue("weight", healthInfoModel.Weight);
                Context.SetValue("calculated_bmi", healthInfoModel.CalculatedBMI);
                Context.SetValue("bmi_category", healthInfoModel.BMICategory);
                Context.SetValue("has_medical_conditions", healthInfoModel.HasMedicalConditions);
                Context.SetValue("health_risk_category", healthInfoModel.HealthRiskCategory);
                Context.SetValue("estimated_premium_multiplier", healthInfoModel.EstimatedPremiumMultiplier);
                Context.SetValue("health_data_quality_score", healthInfoModel.HealthDataQualityScore);
                Context.SetValue("health_data_quality_grade", healthInfoModel.HealthDataQualityGrade);
                Context.SetValue("underwriting_flags", healthInfoModel.UnderwritingFlags);
                Context.SetValue("number_of_medical_conditions", healthInfoModel.NumberOfMedicalConditions);
                
                _logger.LogInformation("[{Topic}] Health Info - Tobacco: {Tobacco}, Conditions: {Conditions}, Risk: {Risk}, Premium Multiplier: {Multiplier}", 
                    Name, healthInfoModel.UsesTobacco, healthInfoModel.NumberOfMedicalConditions, 
                    healthInfoModel.HealthRiskCategory, healthInfoModel.EstimatedPremiumMultiplier);
                    
                // Log BMI if available
                if (healthInfoModel.CalculatedBMI.HasValue)
                {
                    _logger.LogInformation("[{Topic}] BMI Calculated: {BMI} ({Category})", 
                        Name, healthInfoModel.CalculatedBMI.Value, healthInfoModel.BMICategory);
                }
            }
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
    /// Intent detection (keyword matching for health information topics).
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
        Context.SetValue("HealthInfoTopic_runasync", DateTime.UtcNow.ToString("o"));

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