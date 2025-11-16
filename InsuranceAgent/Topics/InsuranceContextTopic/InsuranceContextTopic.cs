using ConversaCore.Models;
using ConversaCore.TopicFlow;

namespace InsuranceAgent.Topics;

/// <summary>
/// Topic for collecting insurance context and financial information.
/// Ported from Copilot Studio adaptive card.
/// Event-driven, queue-based flow of activities.
/// </summary>
public class InsuranceContextTopic : TopicFlow
{
    public const string ActivityId_ShowCard = "ShowInsuranceContextCard";
    public const string ActivityId_DumpCtx = "DumpCTX";
    public const string ActivityId_Trigger = "TriggerNextTopic";

    /// <summary>
    /// Keywords for topic routing.
    /// </summary>
    public static readonly string[] IntentKeywords = new[] {
        "insurance context", "coverage goal", "mortgage protection", "income replacement",
        "home value", "mortgage balance", "equity", "existing coverage", "life insurance",
        "coverage amount", "insurance target", "financial planning", "debt protection",
        "mortgage", "home", "property", "loan", "coverage for", "spouse coverage"
    };

    private readonly ConversaCore.Context.IConversationContext _conversationContext;
    private readonly ILogger<InsuranceContextTopic> _logger;

    public InsuranceContextTopic(
        TopicWorkflowContext context,
        ILogger<InsuranceContextTopic> logger,
        ConversaCore.Context.IConversationContext conversationContext
    ) : base(context, logger, name: "InsuranceContextTopic")
    {
        _logger = logger;
        _conversationContext = conversationContext;

        Context.SetValue("InsuranceContextTopic_create", DateTime.UtcNow.ToString("o"));
        Context.SetValue("TopicName", "Insurance Context");

        // === Activities in queue order ===
        var showCardActivity = new AdaptiveCardActivity<InsuranceContextCard, InsuranceContextModel>(
            ActivityId_ShowCard,
            context,
            cardFactory: card => card.Create(),
            modelContextKey: "InsuranceContextModel",
            onTransition: (from, to, data) => {
                var stamp = DateTime.UtcNow.ToString("o");
                Console.WriteLine(
                    $"[InsuranceContextCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
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
            
            // Store insurance context data in conversation context for other topics to access
            if (e.Model is InsuranceContextModel insuranceContextModel)
            {
                Context.SetValue("insurance_context_data", insuranceContextModel);
                Context.SetValue("insurance_type", insuranceContextModel.InsuranceType);
                Context.SetValue("coverage_for", insuranceContextModel.CoverageFor);
                Context.SetValue("coverage_goal", insuranceContextModel.CoverageGoal);
                Context.SetValue("insurance_target", insuranceContextModel.InsuranceTarget);
                Context.SetValue("parsed_insurance_target", insuranceContextModel.ParsedInsuranceTarget);
                Context.SetValue("home_value", insuranceContextModel.HomeValue);
                Context.SetValue("mortgage_balance", insuranceContextModel.MortgageBalance);
                Context.SetValue("monthly_mortgage", insuranceContextModel.MonthlyMortgage);
                Context.SetValue("loan_term", insuranceContextModel.LoanTerm);
                Context.SetValue("calculated_equity", insuranceContextModel.CalculatedEquity);
                Context.SetValue("loan_to_value_ratio", insuranceContextModel.LoanToValueRatio);
                Context.SetValue("has_existing_life_insurance", insuranceContextModel.HasExistingLifeInsurance);
                Context.SetValue("existing_coverage", insuranceContextModel.ExistingLifeInsuranceCoverage);
                Context.SetValue("parsed_existing_coverage", insuranceContextModel.ParsedExistingCoverage);
                Context.SetValue("additional_coverage_needed", insuranceContextModel.AdditionalCoverageNeeded);
                Context.SetValue("coverage_adequacy_assessment", insuranceContextModel.CoverageAdequacyAssessment);
                Context.SetValue("is_mortgage_protection_goal", insuranceContextModel.IsMortgageProtectionGoal);
                Context.SetValue("is_income_replacement_goal", insuranceContextModel.IsIncomeReplacementGoal);
                Context.SetValue("mortgage_protection_need", insuranceContextModel.MortgageProtectionNeed);
                Context.SetValue("insurance_context_data_quality_score", insuranceContextModel.InsuranceContextDataQualityScore);
                Context.SetValue("insurance_context_data_quality_grade", insuranceContextModel.InsuranceContextDataQualityGrade);
                Context.SetValue("qualification_factors", insuranceContextModel.QualificationFactors);
                Context.SetValue("sales_priority_score", insuranceContextModel.SalesPriorityScore);
                Context.SetValue("sales_priority_level", insuranceContextModel.SalesPriorityLevel);
                
                _logger.LogInformation("[{Topic}] Insurance Context - Type: {Type}, Target: {Target}, Priority: {Priority} ({Score}), Goal: {Goal}", 
                    Name, insuranceContextModel.InsuranceType, insuranceContextModel.ParsedInsuranceTarget, 
                    insuranceContextModel.SalesPriorityLevel, insuranceContextModel.SalesPriorityScore, insuranceContextModel.CoverageGoal);
                    
                // Log financial details if available
                if (insuranceContextModel.HomeValue.HasValue)
                {
                    _logger.LogInformation("[{Topic}] Financial Details - Home: ${Home:N0}, Mortgage: ${Mortgage:N0}, LTV: {LTV:F1}%", 
                        Name, insuranceContextModel.HomeValue.Value, 
                        insuranceContextModel.MortgageBalance ?? 0, 
                        insuranceContextModel.LoanToValueRatio ?? 0);
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
    /// Intent detection (keyword matching for insurance context topics).
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
        Context.SetValue("InsuranceContextTopic_runasync", DateTime.UtcNow.ToString("o"));

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