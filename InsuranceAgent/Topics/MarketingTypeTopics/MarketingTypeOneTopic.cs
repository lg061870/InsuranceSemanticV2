using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Topics.LeadDetailsTopic;
using InsuranceAgent.Topics.LifeGoalsTopic;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace InsuranceAgent.Topics.MarketingTypeTopics
{
    /// <summary>
    /// Topic for handling the full marketing path (T1) with complete lead qualification flow.
    /// This topic orchestrates the sequence of activities for comprehensive lead qualification.
    /// </summary>
    public class MarketingTypeOneTopic : TopicFlow
    {
        public const string ActivityId_LeadDetails = "ShowLeadDetailsCard";
        public const string ActivityId_LifeGoals = "ShowLifeGoalsCard"; 
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_Summary = "ShowSummary";
        public const string ActivityId_NextTopicDecision = "NextTopicDecision";
        public const string ActivityId_TriggerContactInfo = "TriggerContactInfoTopic";
        public const string ActivityId_TriggerDependents = "TriggerDependentsTopic";
        public const string ActivityId_TriggerInsuranceContext = "TriggerInsuranceContextTopic";

        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] {
            "full marketing", "type 1", "t1", "complete path", "full qualification",
            "marketing path one", "lead qualification", "lead details", "life goals"
        };

        private readonly IConversationContext _conversationContext;
        private readonly ILogger<MarketingTypeOneTopic> _logger;

        public MarketingTypeOneTopic(
            TopicWorkflowContext context,
            ILogger<MarketingTypeOneTopic> logger,
            IConversationContext conversationContext
        ) : base(context, logger, name: "MarketingTypeOneTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;

            Context.SetValue("MarketingTypeOneTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("TopicName", "Marketing Path Type 1");
            Context.SetValue("marketing_path_type", "T1");

            // === Activities in queue order ===

            // 1. Lead Details Collection
            var leadDetailsActivity = new AdaptiveCardActivity<LeadDetailsCard, LeadDetailsModel>(
                ActivityId_LeadDetails,
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "LeadDetailsModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    _logger.LogInformation(
                        $"[MarketingTypeOneTopic] {ActivityId_LeadDetails}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
                    );
                },
                customMessage: "Let's start by collecting some basic lead information."
            );

            // 2. Life Goals Collection
            var lifeGoalsActivity = new AdaptiveCardActivity<LifeGoalsCard, LifeGoalsModel>(
                ActivityId_LifeGoals,
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "LifeGoalsModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    _logger.LogInformation(
                        $"[MarketingTypeOneTopic] {ActivityId_LifeGoals}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
                    );
                },
                customMessage: "Great! Now let's understand your goals for life insurance."
            );

            // 3. Summary Activity
            var summaryActivity = new SimpleActivity(ActivityId_Summary, (ctx, input) => {
                var leadDetails = ctx.GetValue<LeadDetailsModel>("LeadDetailsModel");
                var lifeGoals = ctx.GetValue<LifeGoalsModel>("LifeGoalsModel");
                
                var summary = "## Marketing Path Type 1 (Full Marketing) Summary\n\n";
                
                // Lead Details Summary
                summary += "### 📇 Lead Details\n";
                if (leadDetails != null)
                {
                    summary += $"- **Name:** {leadDetails.LeadName}\n";
                    summary += $"- **Interest Level:** {leadDetails.NormalizedInterestLevel}\n";
                    summary += $"- **Lead Intent:** {leadDetails.NormalizedLeadIntent}\n";
                    summary += $"- **Lead Source:** {leadDetails.NormalizedLeadSource}\n";
                    summary += $"- **Priority:** {leadDetails.SalesPriorityLevel}\n";
                    summary += $"- **Quality Score:** {leadDetails.LeadQualificationScore}/100 (Grade {leadDetails.LeadQualificationGrade})\n\n";
                    
                    // Add recommended actions
                    summary += "#### Recommended Actions:\n";
                    foreach (var action in leadDetails.RecommendedActions)
                    {
                        summary += $"- {action}\n";
                    }
                }
                else
                {
                    summary += "- *No lead details collected*\n\n";
                }
                
                // Life Goals Summary
                summary += "\n### 🎯 Life Insurance Goals\n";
                if (lifeGoals != null && lifeGoals.HasSelectedGoals)
                {
                    summary += $"- **Selected Goals:** {string.Join(", ", lifeGoals.SelectedGoals)}\n";
                    summary += $"- **Primary Category:** {lifeGoals.PrimaryGoalCategory}\n";
                    summary += $"- **Recommended Product:** {lifeGoals.RecommendedProductType}\n";
                    summary += $"- **Intent Clarity:** {lifeGoals.IntentClarityLevel}\n";
                    summary += $"- **Coverage Multiplier:** {lifeGoals.CoverageMultiplierFactor:F1}x\n\n";
                    
                    // Add sales approach recommendations
                    summary += "#### Sales Approach Recommendations:\n";
                    foreach (var recommendation in lifeGoals.SalesApproachRecommendations.Take(3))
                    {
                        summary += $"- {recommendation}\n";
                    }
                }
                else
                {
                    summary += "- *No life goals collected*\n\n";
                }
                
                // Next steps
                summary += "\n### 📋 Next Steps\n";
                summary += "- Continue with lead qualification process\n";
                summary += "- Schedule follow-up based on priority level\n";
                summary += "- Match with appropriate product offerings\n";
                
                Context.SetValue("marketing_path_summary", summary);
                
                // Set up the path decision logic
                string routingDecision = "default"; // Default path
                
                if (leadDetails != null)
                {
                    if (leadDetails.IsAppointmentToday || 
                        (leadDetails.NormalizedInterestLevel == "High" && 
                         leadDetails.NormalizedLeadIntent == "Buy"))
                    {
                        // High priority lead - expedite with contact info
                        routingDecision = "high_priority";
                    }
                    else if (lifeGoals != null && lifeGoals.HasProtectionGoals)
                    {
                        // Has protection goals - continue with dependents topic
                        routingDecision = "protection_goals";
                    }
                    else if (lifeGoals != null && lifeGoals.IsUnsureOnly)
                    {
                        // Unsure about goals - provide educational content
                        routingDecision = "needs_education";
                    }
                }
                
                // Store the decision in context for the conditional activity
                context.SetValue("t1_routing_decision", routingDecision);
                
                _logger.LogInformation("[MarketingTypeOneTopic] Generated T1 marketing path summary with routing decision: {Decision}", routingDecision);
                return Task.FromResult<object?>(summary);
            });

            // 4. Conditional Decision for Next Topic
            var nextTopicDecision = ConditionalActivity<TriggerTopicActivity>.Switch(
                ActivityId_NextTopicDecision,
                ctx => ctx.GetValue<string>("t1_routing_decision") ?? "default",
                new Dictionary<string, Func<string, TopicWorkflowContext, TriggerTopicActivity>> {
                    // High priority leads go to contact info collection
                    ["high_priority"] = (id, ctx) => new TriggerTopicActivity(
                        id, 
                        "ContactInfoTopic", 
                        _logger,
                        waitForCompletion: false,
                        conversationContext: _conversationContext
                    ),
                    
                    // Protection goal leads go to dependents topic
                    ["protection_goals"] = (id, ctx) => new TriggerTopicActivity(
                        id, 
                        "DependentsTopic",
                        _logger,
                        waitForCompletion: false,
                        conversationContext: _conversationContext
                    ),
                    
                    // Educational needs go to insurance context topic
                    ["needs_education"] = (id, ctx) => new TriggerTopicActivity(
                        id, 
                        "InsuranceContextTopic",
                        _logger, 
                        waitForCompletion: false,
                        conversationContext: _conversationContext
                    ),
                    
                    // Default path also goes to contact info
                    ["default"] = (id, ctx) => new TriggerTopicActivity(
                        id, 
                        "ContactInfoTopic",
                        _logger,
                        waitForCompletion: false,
                        conversationContext: _conversationContext
                    )
                },
                defaultBranch: "default",
                logger: _logger
            );

            // === Event hooks for AdaptiveCard lifecycle ===

            // Lead Details Activity Events
            leadDetailsActivity.ModelBound += (s, e) =>
            {
                _logger.LogInformation("[MarketingTypeOneTopic] LeadDetails model bound");
                
                if (e.Model is LeadDetailsModel leadDetailsModel)
                {
                    Context.SetValue("lead_details_data", leadDetailsModel);
                    Context.SetValue("lead_name", leadDetailsModel.LeadName);
                    Context.SetValue("lead_source", leadDetailsModel.LeadSource);
                    Context.SetValue("interest_level", leadDetailsModel.InterestLevel);
                    Context.SetValue("lead_intent", leadDetailsModel.LeadIntent);
                    Context.SetValue("lead_qualification_score", leadDetailsModel.LeadQualificationScore);
                    
                    _logger.LogInformation("[MarketingTypeOneTopic] Captured lead details - Name: {Name}, Intent: {Intent}, Priority: {Priority}", 
                        leadDetailsModel.LeadName, leadDetailsModel.NormalizedLeadIntent, leadDetailsModel.SalesPriorityLevel);
                }
            };

            // Life Goals Activity Events
            lifeGoalsActivity.ModelBound += (s, e) =>
            {
                _logger.LogInformation("[MarketingTypeOneTopic] LifeGoals model bound");
                
                if (e.Model is LifeGoalsModel lifeGoalsModel)
                {
                    Context.SetValue("life_goals_data", lifeGoalsModel);
                    Context.SetValue("selected_goals", lifeGoalsModel.SelectedGoals);
                    Context.SetValue("primary_goal_category", lifeGoalsModel.PrimaryGoalCategory);
                    Context.SetValue("recommended_product_type", lifeGoalsModel.RecommendedProductType);
                    Context.SetValue("coverage_multiplier", lifeGoalsModel.CoverageMultiplierFactor);
                    
                    _logger.LogInformation("[MarketingTypeOneTopic] Captured life goals - Goals: {Count}, Primary: {Primary}, Product: {Product}", 
                        lifeGoalsModel.TotalGoalsSelected, lifeGoalsModel.PrimaryGoalCategory, lifeGoalsModel.RecommendedProductType);
                }
            };

            // Topic trigger event forwarding
            nextTopicDecision.TopicTriggered += (sender, e) =>
            {
                _logger.LogInformation("[MarketingTypeOneTopic] Triggering next topic via conditional: {Next}", e.TopicName);
                _conversationContext.AddTopicToChain(e.TopicName);
            };

            // === Enqueue activities ===
            Add(leadDetailsActivity);
            Add(lifeGoalsActivity);
            Add(summaryActivity);
            Add(nextTopicDecision);
            
            // Development environment context dumping
            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            if (isDevelopment)
            {
                Add(new DumpCtxActivity(ActivityId_DumpCtx, true));
            }
        }

        /// <summary>
        /// Intent detection (keyword matching for marketing path topics).
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
            
            _logger.LogDebug("[MarketingTypeOneTopic] Intent confidence: {Confidence} for message: {Message}", 
                confidence, message);
                
            return Task.FromResult(confidence);
        }

        /// <summary>
        /// Execute the topic's activities in queue order.
        /// Also handles optional NextTopic context handoff.
        /// </summary>
        public override async Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
        {
            Context.SetValue("MarketingTypeOneTopic_runasync", DateTime.UtcNow.ToString("o"));

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