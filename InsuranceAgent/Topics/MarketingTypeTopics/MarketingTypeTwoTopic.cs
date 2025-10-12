using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Topics.LeadDetailsTopic;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InsuranceAgent.Topics.MarketingTypeTopics
{
    /// <summary>
    /// Topic for handling the partial marketing path (T2) with limited lead qualification flow.
    /// This path is for leads with partial marketing consent (TCPA only).
    /// </summary>
    public class MarketingTypeTwoTopic : TopicFlow
    {
        public const string ActivityId_LeadDetails = "ShowLeadDetailsCard";
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_Summary = "ShowSummary";
        public const string ActivityId_NextTopicDecision = "NextTopicDecision";
        public const string ActivityId_TriggerContactInfo = "TriggerContactInfoTopic";
        public const string ActivityId_TriggerInsuranceContext = "TriggerInsuranceContextTopic";

        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] {
            "partial marketing", "type 2", "t2", "tcpa only", "partial path",
            "marketing path two", "limited qualification", "lead details only"
        };

        private readonly IConversationContext _conversationContext;
        private readonly ILogger<MarketingTypeTwoTopic> _logger;

        public MarketingTypeTwoTopic(
            TopicWorkflowContext context,
            ILogger<MarketingTypeTwoTopic> logger,
            IConversationContext conversationContext
        ) : base(context, logger, name: "MarketingTypeTwoTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;

            Context.SetValue("MarketingTypeTwoTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("TopicName", "Marketing Path Type 2");
            Context.SetValue("marketing_path_type", "T2");

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
                        $"[MarketingTypeTwoTopic] {ActivityId_LeadDetails}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
                    );
                },
                customMessage: "Let's collect some basic lead information. (T2 Path - TCPA consent only)"
            );

            // 2. Summary Activity
            var summaryActivity = new SimpleActivity(ActivityId_Summary, (ctx, input) => {
                var leadDetails = ctx.GetValue<LeadDetailsModel>("LeadDetailsModel");
                
                var summary = "## Marketing Path Type 2 (TCPA Only) Summary\n\n";
                
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
                
                // Next steps for T2 path
                summary += "\n### 📋 Next Steps\n";
                summary += "- Continue with limited lead qualification process\n";
                summary += "- Note: This is a T2 path with TCPA consent only\n";
                summary += "- Limited marketing follow-up options available\n";
                
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
                    else if (leadDetails.NormalizedInterestLevel == "Low" || 
                             leadDetails.LeadQualificationScore < 40)
                    {
                        // Low interest/qualification - provide educational content
                        routingDecision = "needs_education";
                    }
                }
                
                // Store the decision in context for the conditional activity
                context.SetValue("t2_routing_decision", routingDecision);
                
                _logger.LogInformation("[MarketingTypeTwoTopic] Generated T2 marketing path summary with routing decision: {Decision}", routingDecision);
                return Task.FromResult<object?>(summary);
            });

            // 3. Conditional Decision for Next Topic
            var nextTopicDecision = ConditionalActivity<TriggerTopicActivity>.Switch(
                ActivityId_NextTopicDecision,
                ctx => ctx.GetValue<string>("t2_routing_decision") ?? "default",
                new Dictionary<string, Func<string, TopicWorkflowContext, TriggerTopicActivity>> {
                    // High priority leads go to contact info collection
                    ["high_priority"] = (id, ctx) => new TriggerTopicActivity(
                        id, 
                        "ContactInfoTopic", 
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
                _logger.LogInformation("[MarketingTypeTwoTopic] LeadDetails model bound");
                
                if (e.Model is LeadDetailsModel leadDetailsModel)
                {
                    Context.SetValue("lead_details_data", leadDetailsModel);
                    Context.SetValue("lead_name", leadDetailsModel.LeadName);
                    Context.SetValue("lead_source", leadDetailsModel.LeadSource);
                    Context.SetValue("interest_level", leadDetailsModel.InterestLevel);
                    Context.SetValue("lead_intent", leadDetailsModel.LeadIntent);
                    Context.SetValue("lead_qualification_score", leadDetailsModel.LeadQualificationScore);
                    
                    _logger.LogInformation("[MarketingTypeTwoTopic] Captured lead details - Name: {Name}, Intent: {Intent}, Priority: {Priority}", 
                        leadDetailsModel.LeadName, leadDetailsModel.NormalizedLeadIntent, leadDetailsModel.SalesPriorityLevel);
                }
            };

            // Topic trigger event forwarding
            nextTopicDecision.TopicTriggered += (sender, e) =>
            {
                _logger.LogInformation("[MarketingTypeTwoTopic] Triggering next topic via conditional: {Next}", e.TopicName);
                _conversationContext.AddTopicToChain(e.TopicName);
            };

            // === Enqueue activities ===
            Add(leadDetailsActivity);
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
            
            _logger.LogDebug("[MarketingTypeTwoTopic] Intent confidence: {Confidence} for message: {Message}", 
                confidence, message);
                
            return Task.FromResult(confidence);
        }

        /// <summary>
        /// Execute the topic's activities in queue order.
        /// Also handles optional NextTopic context handoff.
        /// </summary>
        public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
        {
            Context.SetValue("MarketingTypeTwoTopic_runasync", DateTime.UtcNow.ToString("o"));

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