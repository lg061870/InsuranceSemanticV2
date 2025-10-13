using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Topics.LifeGoalsTopic;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Topics.LifeGoalsTopic
{
    /// <summary>
    /// Topic for collecting life insurance goals and intentions.
    /// Ported from Copilot Studio adaptive card.
    /// Event-driven, queue-based flow of activities.
    /// </summary>
    public class LifeGoalsTopic : TopicFlow
    {
        public const string ActivityId_ShowCard = "ShowLifeGoalsCard";
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_Trigger = "TriggerNextTopic";

        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] {
            "life insurance goals", "protect loved ones", "pay mortgage", "family future",
            "peace of mind", "final expenses", "coverage goals", "insurance intent",
            "mortgage protection", "family protection", "burial insurance", "death benefit",
            "beneficiaries", "financial security", "income replacement", "debt protection",
            "legacy planning", "estate planning", "what are your goals", "why life insurance",
            "insurance needs", "coverage purpose", "protection needs", "unsure about insurance"
        };

        private readonly ConversaCore.Context.IConversationContext _conversationContext;
        private readonly ILogger<LifeGoalsTopic> _logger;

        public LifeGoalsTopic(
            TopicWorkflowContext context,
            ILogger<LifeGoalsTopic> logger,
            ConversaCore.Context.IConversationContext conversationContext
        ) : base(context, logger, name: "LifeGoalsTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;

            Context.SetValue("LifeGoalsTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("TopicName", "Life Insurance Goals");

            // === Activities in queue order ===
            var showCardActivity = new AdaptiveCardActivity<LifeGoalsCard, LifeGoalsModel>(
                ActivityId_ShowCard,
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "LifeGoalsModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    Console.WriteLine(
                        $"[LifeGoalsCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
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
                
                // Store life goals data in conversation context for product recommendations and needs analysis
                if (e.Model is LifeGoalsModel lifeGoalsModel)
                {
                    Context.SetValue("life_goals_data", lifeGoalsModel);
                    Context.SetValue("protect_loved_ones", lifeGoalsModel.ProtectLovedOnes);
                    Context.SetValue("pay_mortgage", lifeGoalsModel.PayMortgage);
                    Context.SetValue("prepare_future", lifeGoalsModel.PrepareFuture);
                    Context.SetValue("peace_of_mind", lifeGoalsModel.PeaceOfMind);
                    Context.SetValue("cover_expenses", lifeGoalsModel.CoverExpenses);
                    Context.SetValue("unsure", lifeGoalsModel.Unsure);
                    Context.SetValue("selected_goals", lifeGoalsModel.SelectedGoals);
                    Context.SetValue("total_goals_selected", lifeGoalsModel.TotalGoalsSelected);
                    Context.SetValue("has_selected_goals", lifeGoalsModel.HasSelectedGoals);
                    Context.SetValue("has_multiple_goals", lifeGoalsModel.HasMultipleGoals);
                    Context.SetValue("is_unsure_only", lifeGoalsModel.IsUnsureOnly);
                    Context.SetValue("has_specific_goals", lifeGoalsModel.HasSpecificGoals);
                    Context.SetValue("has_protection_goals", lifeGoalsModel.HasProtectionGoals);
                    Context.SetValue("has_financial_goals", lifeGoalsModel.HasFinancialGoals);
                    Context.SetValue("has_emotional_goals", lifeGoalsModel.HasEmotionalGoals);
                    Context.SetValue("has_practical_goals", lifeGoalsModel.HasPracticalGoals);
                    Context.SetValue("has_family_focused_goals", lifeGoalsModel.HasFamilyFocusedGoals);
                    Context.SetValue("intent_clarity_score", lifeGoalsModel.IntentClarityScore);
                    Context.SetValue("intent_clarity_level", lifeGoalsModel.IntentClarityLevel);
                    Context.SetValue("primary_goal_category", lifeGoalsModel.PrimaryGoalCategory);
                    Context.SetValue("term_life_affinity_score", lifeGoalsModel.TermLifeAffinityScore);
                    Context.SetValue("whole_life_affinity_score", lifeGoalsModel.WholeLifeAffinityScore);
                    Context.SetValue("universal_life_affinity_score", lifeGoalsModel.UniversalLifeAffinityScore);
                    Context.SetValue("recommended_product_type", lifeGoalsModel.RecommendedProductType);
                    Context.SetValue("coverage_multiplier_factor", lifeGoalsModel.CoverageMultiplierFactor);
                    Context.SetValue("urgency_level", lifeGoalsModel.UrgencyLevel);
                    Context.SetValue("sales_approach_recommendations", lifeGoalsModel.SalesApproachRecommendations);
                    Context.SetValue("goal_insights", lifeGoalsModel.GoalInsights);
                    Context.SetValue("goal_data_quality_score", lifeGoalsModel.GoalDataQualityScore);
                    Context.SetValue("goal_data_quality_grade", lifeGoalsModel.GoalDataQualityGrade);
                    
                    _logger.LogInformation("[{Topic}] Life Goals - Selected: {Count} goals, Primary: {Primary}, Product: {Product}, Clarity: {Clarity}", 
                        Name, lifeGoalsModel.TotalGoalsSelected, lifeGoalsModel.PrimaryGoalCategory, 
                        lifeGoalsModel.RecommendedProductType, lifeGoalsModel.IntentClarityLevel);
                        
                    // Log specific goal combinations
                    if (lifeGoalsModel.PayMortgage && lifeGoalsModel.ProtectLovedOnes)
                    {
                        _logger.LogInformation("[{Topic}] 🏠👨‍👩‍👧‍👦 High Priority: Mortgage protection + family protection goals", Name);
                    }
                    
                    if (lifeGoalsModel.IsUnsureOnly)
                    {
                        _logger.LogInformation("[{Topic}] 💭 Education Opportunity: Client unsure about goals - needs consultation", Name);
                    }
                    
                    if (lifeGoalsModel.HasMultipleGoals && !lifeGoalsModel.Unsure)
                    {
                        _logger.LogInformation("[{Topic}] 📈 Comprehensive Need: {Goals} suggests higher coverage requirement", 
                            Name, string.Join(", ", lifeGoalsModel.SelectedGoals));
                    }
                    
                    // Log product affinity insights
                    _logger.LogInformation("[{Topic}] Product Affinity - Term: {Term}, Whole: {Whole}, Universal: {Universal}", 
                        Name, lifeGoalsModel.TermLifeAffinityScore, lifeGoalsModel.WholeLifeAffinityScore, lifeGoalsModel.UniversalLifeAffinityScore);
                    
                    // Log urgency and sales approach
                    if (lifeGoalsModel.UrgencyLevel.Contains("High"))
                    {
                        _logger.LogWarning("[{Topic}] 🚨 HIGH URGENCY: {Urgency} - prioritize immediate follow-up", 
                            Name, lifeGoalsModel.UrgencyLevel);
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
        /// Intent detection (keyword matching for life insurance goals topics).
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
            Context.SetValue("LifeGoalsTopic_runasync", DateTime.UtcNow.ToString("o"));

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
}