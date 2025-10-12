using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Topics.DependentsTopic;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Topics.DependentsTopic
{
    /// <summary>
    /// Topic for collecting financial dependents information.
    /// Ported from Copilot Studio adaptive card.
    /// Event-driven, queue-based flow of activities.
    /// </summary>
    public class DependentsTopic : TopicFlow
    {
        public const string ActivityId_ShowCard = "ShowDependentsCard";
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_Trigger = "TriggerNextTopic";

        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] {
            "dependents", "family", "children", "spouse", "married", "single",
            "divorced", "widowed", "partnered", "marital status", "kids",
            "financial dependents", "family members", "beneficiaries", "dependents",
            "child", "son", "daughter", "husband", "wife", "partner", "family size"
        };

        private readonly ConversaCore.Context.IConversationContext _conversationContext;
        private readonly ILogger<DependentsTopic> _logger;

        public DependentsTopic(
            TopicWorkflowContext context,
            ILogger<DependentsTopic> logger,
            ConversaCore.Context.IConversationContext conversationContext
        ) : base(context, logger, name: "DependentsTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;

            Context.SetValue("DependentsTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("TopicName", "Financial Dependents");

            // === Activities in queue order ===
            var showCardActivity = new AdaptiveCardActivity<DependentsCard, DependentsModel>(
                ActivityId_ShowCard,
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "DependentsModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    Console.WriteLine(
                        $"[DependentsCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
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
                
                // Store dependents data in conversation context for other topics to access
                if (e.Model is DependentsModel dependentsModel)
                {
                    Context.SetValue("dependents_data", dependentsModel);
                    Context.SetValue("marital_status", dependentsModel.MaritalStatus);
                    Context.SetValue("has_dependents", dependentsModel.HasDependents);
                    Context.SetValue("selected_age_ranges", dependentsModel.SelectedAgeRanges);
                    Context.SetValue("is_married_or_partnered", dependentsModel.IsMarriedOrPartnered);
                    Context.SetValue("is_single_parent", dependentsModel.IsSingleParent);
                    Context.SetValue("has_young_children", dependentsModel.HasYoungChildren);
                    Context.SetValue("has_teenage_children", dependentsModel.HasTeenageChildren);
                    Context.SetValue("has_adult_children", dependentsModel.HasAdultChildren);
                    Context.SetValue("financial_responsibility_level", dependentsModel.FinancialResponsibilityLevel);
                    Context.SetValue("life_insurance_need_level", dependentsModel.LifeInsuranceNeedLevel);
                    Context.SetValue("life_insurance_multiplier", dependentsModel.LifeInsuranceMultiplier);
                    Context.SetValue("estimated_number_of_dependents", dependentsModel.EstimatedNumberOfDependents);
                    Context.SetValue("dependents_data_quality_score", dependentsModel.DependentsDataQualityScore);
                    Context.SetValue("dependents_data_quality_grade", dependentsModel.DependentsDataQualityGrade);
                    Context.SetValue("underwriting_risk_factors", dependentsModel.UnderwritingRiskFactors);
                    
                    _logger.LogInformation("[{Topic}] Dependents - Marital: {Marital}, Has Deps: {HasDeps}, Need Level: {NeedLevel}, Responsibility: {Responsibility}", 
                        Name, dependentsModel.MaritalStatus, dependentsModel.HasDependents, 
                        dependentsModel.LifeInsuranceNeedLevel, dependentsModel.FinancialResponsibilityLevel);
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
        /// Intent detection (keyword matching for dependents/family topics).
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
            Context.SetValue("DependentsTopic_runasync", DateTime.UtcNow.ToString("o"));

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
}