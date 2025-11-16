using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;

namespace InsuranceAgent.Topics.MarketingTypeTopics {
    /// <summary>
    /// Handles the partial marketing path (T2) with limited lead qualification flow (TCPA only).
    /// </summary>
    public class MarketingT2Topic : TopicFlow {
        public const string ActivityId_LeadDetails = "ShowLeadDetailsCard";
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_Summary = "ShowSummary";
        public const string ActivityId_NextTopicDecision = "NextTopicDecision";
        public const string ActivityId_TriggerContactInfo = "TriggerContactInfoTopic";
        public const string ActivityId_TriggerInsuranceContext = "TriggerInsuranceContextTopic";
        public const string ActivityId_ShowCustomerConsole = "ShowCustomerConsole";

        public static readonly string[] IntentKeywords = new[] {
            "partial marketing", "type 2", "t2", "tcpa only", "partial path",
            "marketing path two", "limited qualification", "lead details only"
        };

        private readonly IConversationContext _conversationContext;
        private readonly ILogger<MarketingT2Topic> _logger;

        public MarketingT2Topic(
            TopicWorkflowContext context,
            ILogger<MarketingT2Topic> logger,
            IConversationContext conversationContext)
            : base(context, logger, name: "MarketingTypeTwoTopic") {

            _logger = logger;
            _conversationContext = conversationContext;

            Context.SetValue("MarketingTypeTwoTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("TopicName", "Marketing Path Type 2");
            Context.SetValue("marketing_path_type", "T2");

            InitializeActivities();
        }

        private void InitializeActivities() {
            ClearActivities();

            // === 1. Lead Details Collection ===
            var leadDetailsActivity = new AdaptiveCardActivity<LeadDetailsCard, LeadDetailsModel>(
                ActivityId_LeadDetails,
                Context,
                cardFactory: card => card.Create(),
                modelContextKey: "LeadDetailsModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    _logger.LogInformation(
                        $"[MarketingT2Topic] {ActivityId_LeadDetails}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
                    );
                },
                customMessage: "Let's collect some basic lead information. (T2 Path - TCPA consent only)"
            );

            // === 2. Customer Console (realtime dashboard) ===
            var showCustomerConsole = new EventTriggerActivity(
                id: ActivityId_ShowCustomerConsole,
                eventName: "ui.dashboard.show",
                eventData: new {
                    dashboardType = "customer-console",
                    userPath = "marketing-t2",
                    progressStage = "qualification-started",
                    timestamp = DateTime.UtcNow,
                    context = new {
                        domain = "insurance",
                        flowType = "lead-qualification",
                        consentLevel = "partial" // TCPA only
                    }
                },
                waitForResponse: false,
                logger: _logger,
                conversationContext: _conversationContext
            );

            // === 3. Summary ===
            var summaryActivity = new SimpleActivity(ActivityId_Summary, (ctx, input) => {
                var leadDetails = ctx.GetValue<LeadDetailsModel>("LeadDetailsModel");
                var summary = "## Marketing Path Type 2 (TCPA Only) Summary\n\n";

                summary += "### 📇 Lead Details\n";
                if (leadDetails != null) {
                    summary += $"- **Name:** {leadDetails.LeadName}\n";
                    summary += $"- **Interest Level:** {leadDetails.NormalizedInterestLevel}\n";
                    summary += $"- **Lead Intent:** {leadDetails.NormalizedLeadIntent}\n";
                    summary += $"- **Lead Source:** {leadDetails.NormalizedLeadSource}\n";
                    summary += $"- **Priority:** {leadDetails.SalesPriorityLevel}\n";
                    summary += $"- **Quality Score:** {leadDetails.LeadQualificationScore}/100 (Grade {leadDetails.LeadQualificationGrade})\n\n";
                    summary += "#### Recommended Actions:\n";
                    foreach (var action in leadDetails.RecommendedActions)
                        summary += $"- {action}\n";
                }
                else {
                    summary += "- *No lead details collected*\n\n";
                }

                summary += "\n### 📋 Next Steps\n";
                summary += "- Continue with limited lead qualification process\n";
                summary += "- Note: This is a T2 path with TCPA consent only\n";
                summary += "- Limited marketing follow-up options available\n";

                ctx.SetValue("marketing_path_summary", summary);

                // Determine next routing decision
                string decision = "default";
                if (leadDetails != null) {
                    if (leadDetails.IsAppointmentToday ||
                        (leadDetails.NormalizedInterestLevel == "High" && leadDetails.NormalizedLeadIntent == "Buy"))
                        decision = "high_priority";
                    else if (leadDetails.NormalizedInterestLevel == "Low" || leadDetails.LeadQualificationScore < 40)
                        decision = "needs_education";
                }

                ctx.SetValue("t2_routing_decision", decision);
                _logger.LogInformation("[MarketingT2Topic] Generated summary with routing decision: {Decision}", decision);
                return Task.FromResult<object?>(summary);
            });

            // === 4. Conditional Routing ===
            var nextTopicDecision = ConditionalActivity<TriggerTopicActivity>.Switch(
                ActivityId_NextTopicDecision,
                ctx => ctx.GetValue<string>("t2_routing_decision") ?? "default",
                new Dictionary<string, Func<string, TopicWorkflowContext, TriggerTopicActivity>> {
                    ["high_priority"] = (id, ctx) => new TriggerTopicActivity(
                        id, "ContactInfoTopic", _logger, waitForCompletion: false, conversationContext: _conversationContext),
                    ["needs_education"] = (id, ctx) => new TriggerTopicActivity(
                        id, "InsuranceContextTopic", _logger, waitForCompletion: false, conversationContext: _conversationContext),
                    ["default"] = (id, ctx) => new TriggerTopicActivity(
                        id, "ContactInfoTopic", _logger, waitForCompletion: false, conversationContext: _conversationContext)
                },
                defaultBranch: "default",
                logger: _logger
            );

            nextTopicDecision.TopicTriggered += (sender, e) => {
                _logger.LogInformation("[MarketingT2Topic] Triggering next topic: {Next}", e.TopicName);
                _conversationContext.AddTopicToChain(e.TopicName);
            };

            // === 5. Progress Events (simplified flow) ===
            var progressAfterLead = new EventTriggerActivity(
                id: "ProgressAfterLeadDetails",
                eventName: "ui.progress.update",
                eventData: new {
                    stage = "lead-details-completed",
                    progress = 50,
                    message = "Lead information collected (T2 path)",
                    nextStep = "summary",
                    timestamp = DateTime.UtcNow
                },
                waitForResponse: false,
                logger: _logger,
                conversationContext: _conversationContext
            );

            var qualificationComplete = new EventTriggerActivity(
                id: "QualificationComplete",
                eventName: "ui.progress.complete",
                eventData: new {
                    stage = "qualification-finished",
                    progress = 100,
                    message = "T2 qualification completed",
                    nextStep = "next-topic",
                    timestamp = DateTime.UtcNow
                },
                waitForResponse: false,
                logger: _logger,
                conversationContext: _conversationContext
            );

            // === Enqueue All Activities ===
            Add(leadDetailsActivity);
            Add(showCustomerConsole);
            Add(progressAfterLead);
            Add(summaryActivity);
            Add(qualificationComplete);
            Add(nextTopicDecision);

            // === Optional Dev Dump ===
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development") {
                Add(new DumpCtxActivity(ActivityId_DumpCtx, true));
            }

            // === Bind LeadDetails Model Event ===
            leadDetailsActivity.ModelBound += (s, e) => {
                _logger.LogInformation("[MarketingT2Topic] LeadDetails model bound");
                if (e.Model is LeadDetailsModel model) {
                    Context.SetValue("lead_details_data", model);
                    Context.SetValue("lead_name", model.LeadName);
                    Context.SetValue("lead_source", model.LeadSource);
                    Context.SetValue("interest_level", model.InterestLevel);
                    Context.SetValue("lead_intent", model.LeadIntent);
                    Context.SetValue("lead_qualification_score", model.LeadQualificationScore);
                    _logger.LogInformation("[MarketingT2Topic] Captured lead: {Name}, Intent: {Intent}, Priority: {Priority}",
                        model.LeadName, model.NormalizedLeadIntent, model.SalesPriorityLevel);
                }
            };
        }

        public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(message)) return Task.FromResult(0f);
            var msg = message.ToLowerInvariant();
            var matchCount = IntentKeywords.Count(msg.Contains);
            var confidence = matchCount > 0 ? Math.Min(1.0f, matchCount / 3.0f) : 0f;

            _logger.LogDebug("[MarketingT2Topic] Intent confidence: {Confidence} for message: {Message}", confidence, message);
            return Task.FromResult(confidence);
        }

        public override async Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
            Context.SetValue("MarketingTypeTwoTopic_runasync", DateTime.UtcNow.ToString("o"));
            var result = await base.RunAsync(cancellationToken);

            var nextTopic = Context.GetValue<string>("NextTopic");
            if (!string.IsNullOrEmpty(nextTopic)) {
                result.NextTopicName = nextTopic;
                Context.SetValue("NextTopic", null);
            }

            return result;
        }
    }
}
