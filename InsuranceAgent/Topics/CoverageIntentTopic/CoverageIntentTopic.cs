using ConversaCore.Models;
using ConversaCore.TopicFlow;

namespace InsuranceAgent.Topics
{
    /// <summary>
    /// Topic for collecting coverage intent and preferences.
    /// Ported from Copilot Studio adaptive card.
    /// Event-driven, queue-based flow of activities.
    /// </summary>
    public class CoverageIntentTopic : TopicFlow
    {
        public const string ActivityId_ShowCard = "ShowCoverageIntentCard";
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_Trigger = "TriggerNextTopic";

        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] {
            "coverage", "coverage intent", "insurance type", "term life", "whole life",
            "final expense", "health insurance", "medicare", "coverage amount",
            "coverage start", "when start", "how much coverage", "insurance planning",
            "life insurance", "death benefit", "policy amount", "premium", "quotes"
        };

        private readonly ConversaCore.Context.IConversationContext _conversationContext;
        private readonly ILogger<CoverageIntentTopic> _logger;

        public CoverageIntentTopic(
            TopicWorkflowContext context,
            ILogger<CoverageIntentTopic> logger,
            ConversaCore.Context.IConversationContext conversationContext
        ) : base(context, logger, name: "CoverageIntentTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;

            Context.SetValue("CoverageIntentTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("TopicName", "Coverage Intent");

            // === Activities in queue order ===
            var showCardActivity = new AdaptiveCardActivity<CoverageIntentCard, CoverageIntentModel>(
                ActivityId_ShowCard,
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "CoverageIntentModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    Console.WriteLine(
                        $"[CoverageIntentCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
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
                
                // Store coverage intent data in conversation context for other topics to access
                if (e.Model is CoverageIntentModel coverageIntentModel)
                {
                    Context.SetValue("coverage_intent_data", coverageIntentModel);
                    Context.SetValue("selected_coverage_types", coverageIntentModel.SelectedCoverageTypes);
                    Context.SetValue("preferred_start_time", coverageIntentModel.PreferredCoverageStartTime);
                    Context.SetValue("desired_coverage_amount_band", coverageIntentModel.DesiredCoverageAmountBand);
                    Context.SetValue("estimated_coverage_amount", coverageIntentModel.EstimatedCoverageAmount);
                    Context.SetValue("is_life_insurance_interest", coverageIntentModel.IsLifeInsuranceInterest);
                    Context.SetValue("is_health_insurance_interest", coverageIntentModel.IsHealthInsuranceInterest);
                    Context.SetValue("is_urgent_timeframe", coverageIntentModel.IsUrgentTimeframe);
                    Context.SetValue("is_high_value_coverage", coverageIntentModel.IsHighValueCoverage);
                    Context.SetValue("intent_clarity_score", coverageIntentModel.IntentClarityScore);
                    Context.SetValue("intent_clarity_grade", coverageIntentModel.IntentClarityGrade);
                    Context.SetValue("sales_priority_score", coverageIntentModel.SalesPriorityScore);
                    Context.SetValue("sales_priority_level", coverageIntentModel.SalesPriorityLevel);
                    
                    _logger.LogInformation("[{Topic}] Coverage Intent - Types: {Types}, Priority: {Priority} ({Score}), Amount: {Amount}", 
                        Name, string.Join(", ", coverageIntentModel.SelectedCoverageTypes), 
                        coverageIntentModel.SalesPriorityLevel, coverageIntentModel.SalesPriorityScore,
                        coverageIntentModel.DesiredCoverageAmountBand);
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
        /// Intent detection (keyword matching for coverage intent topics).
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
            Context.SetValue("CoverageIntentTopic_runasync", DateTime.UtcNow.ToString("o"));

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