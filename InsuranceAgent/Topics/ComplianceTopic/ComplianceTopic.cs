using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Topics.ComplianceTopic;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Topic for collecting TCPA consent and CCPA compliance acknowledgment.
    /// Ported from Copilot Studio adaptive card.
    /// Event-driven, queue-based flow of activities.
    /// </summary>
    public class ComplianceTopic : TopicFlow
    {
        public const string ActivityId_ShowCard = "ShowComplianceCard";
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_Trigger = "TriggerNextTopic";

        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] {
            "consent", "compliance", "tcpa", "ccpa", "privacy", "contact permission",
            "legal consent", "agreement", "terms", "privacy notice"
        };

        private readonly ConversaCore.Context.IConversationContext _conversationContext;
        private readonly ILogger<ComplianceTopic> _logger;

        public ComplianceTopic(
            TopicWorkflowContext context,
            ILogger<ComplianceTopic> logger,
            ConversaCore.Context.IConversationContext conversationContext
        ) : base(context, logger, name: "ComplianceTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;

            Context.SetValue("ComplianceTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("TopicName", "Compliance & Consent");

            // === Activities in queue order ===
            var showCardActivity = new AdaptiveCardActivity<ComplianceCard, ComplianceModel>(
                ActivityId_ShowCard,
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "ComplianceModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    Console.WriteLine(
                        $"[ComplianceCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
                    );
                }
            );

            var isDevelopment =
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            var dumpCtxActivity = new DumpCtxActivity(ActivityId_DumpCtx, isDevelopment);

            // === TCPA/CCPA Decision Matrix ===
            // Route users based on TCPA consent + CCPA acknowledgment combinations
            // Insurance compliance: "don't want to answer" = "no" for marketing purposes
            
            // Create individual scenario activities
            var highIntentActivity = new AdaptiveCardActivity<HighIntentScenarioCard, HighIntentScenarioModel>(
                "HighIntentScenario",
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "HighIntentScenarioModel"
            );
            
            var mediumIntentActivity = new AdaptiveCardActivity<MediumIntentScenarioCard, MediumIntentScenarioModel>(
                "MediumIntentScenario",
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "MediumIntentScenarioModel"
            );
            
            var lowIntentActivity = new AdaptiveCardActivity<LowIntentScenarioCard, LowIntentScenarioModel>(
                "LowIntentScenario",
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "LowIntentScenarioModel"
            );
            
            var blockedScenarioActivity = new AdaptiveCardActivity<BlockedScenarioCard, BlockedScenarioModel>(
                "BlockedScenario",
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "BlockedScenarioModel"
            );

            // Main TCPA decision branch
            var complianceDecisionMatrix = ConditionalActivity<TopicFlowActivity>.If(
                "TCPAConsentCheck",
                condition: ctx => ctx.GetValue<bool?>("tcpa_consent") == true,
                
                // TCPA = YES: User consented to calls/texts - show high engagement options
                trueFactory: (id, ctx) => highIntentActivity,
                
                // TCPA = NO/Don't Answer: Route based on CCPA
                falseFactory: (id, ctx) => ConditionalActivity<TopicFlowActivity>.If(
                    "TCPA_No_CCPA_Check",
                    condition: ctx2 => ctx2.GetValue<bool?>("ccpa_acknowledgment") == true,
                    
                    // TCPA = No, CCPA = Yes: Medium intent - educational content + optional contact
                    trueFactory: (id2, ctx2) => mediumIntentActivity,
                    
                    // TCPA = No, CCPA = No/Don't Answer: Route to low/blocked scenarios
                    falseFactory: (id2, ctx2) => ConditionalActivity<TopicFlowActivity>.If(
                        "TCPA_No_CCPA_No_FinalCheck",
                        condition: ctx3 => ctx3.GetValue<bool?>("ccpa_acknowledgment") == false,
                        
                        // TCPA = No, CCPA = No: Low intent - minimal educational content
                        trueFactory: (id3, ctx3) => lowIntentActivity,
                        
                        // TCPA = No, CCPA = Don't Answer: Blocked scenario - basic navigation only
                        falseFactory: (id3, ctx3) => blockedScenarioActivity,
                        logger: _logger
                    ),
                    logger: _logger
                ),
                logger: _logger
            );

            var triggerActivity = new TriggerTopicActivity(
                ActivityId_Trigger,
                "NextTopicName" // Will be set in context or default to next logical topic
            );

            // === Event hooks for scenario activities to handle "change_consent" ===
            EventHandler<ConversaCore.Events.ModelBoundEventArgs> handleChangeConsent = (sender, e) =>
            {
                var modelType = e.Model?.GetType().Name;
                _logger.LogInformation("[{Topic}] Scenario model bound: {ModelType}", Name, modelType);
                
                // Check if user wants to change consent preferences
                var userChoice = "";
                switch (e.Model)
                {
                    case HighIntentScenarioModel highModel:
                        userChoice = highModel.UserChoice ?? "";
                        break;
                    case MediumIntentScenarioModel mediumModel:
                        userChoice = mediumModel.UserChoice ?? "";
                        break;
                    case LowIntentScenarioModel lowModel:
                        userChoice = lowModel.UserChoice ?? "";
                        break;
                    case BlockedScenarioModel blockedModel:
                        userChoice = blockedModel.UserChoice ?? "";
                        break;
                }
                
                if (userChoice == "change_consent")
                {
                    _logger.LogInformation("[{Topic}] User wants to change consent - restarting compliance flow", Name);
                    
                    // Clear existing compliance data
                    Context.SetValue("tcpa_consent", null);
                    Context.SetValue("ccpa_acknowledgment", null);
                    Context.SetValue("compliance_data", null);
                    
                    // Set flag to restart the compliance flow
                    Context.SetValue("restart_compliance", true);
                }
            };
            
            // Subscribe all scenario activities to the change consent handler
            highIntentActivity.ModelBound += handleChangeConsent;
            mediumIntentActivity.ModelBound += handleChangeConsent;
            lowIntentActivity.ModelBound += handleChangeConsent;
            blockedScenarioActivity.ModelBound += handleChangeConsent;

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
                
                // Store compliance data in conversation context for other topics to access
                if (e.Model is ComplianceModel complianceModel)
                {
                    Context.SetValue("compliance_data", complianceModel);
                    Context.SetValue("tcpa_consent", complianceModel.HasTcpaConsent);
                    Context.SetValue("ccpa_acknowledgment", complianceModel.HasCcpaAcknowledgment);
                    
                    _logger.LogInformation("[{Topic}] Compliance status - TCPA: {TcpaConsent}, CCPA: {CcpaAck}", 
                        Name, complianceModel.HasTcpaConsent, complianceModel.HasCcpaAcknowledgment);
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
            Add(dumpCtxActivity);
            Add(complianceDecisionMatrix);
            Add(triggerActivity);
        }

        /// <summary>
        /// Intent detection (keyword matching for compliance/consent topics).
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
        /// Also handles optional NextTopic context handoff and restart compliance flow.
        /// </summary>
        public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
        {
            Context.SetValue("ComplianceTopic_runasync", DateTime.UtcNow.ToString("o"));

            var task = base.RunAsync(cancellationToken);

            return task.ContinueWith(t => {
                var result = t.Result;

                // Check if user wants to restart compliance flow
                var restartCompliance = Context.GetValue<bool>("restart_compliance");
                if (restartCompliance)
                {
                    _logger.LogInformation("[{Topic}] Restarting compliance flow per user request", Name);
                    
                    // Clear the restart flag
                    Context.SetValue("restart_compliance", false);
                    
                    // Set result to restart this topic
                    result.NextTopicName = "ComplianceTopic";
                    return result;
                }

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