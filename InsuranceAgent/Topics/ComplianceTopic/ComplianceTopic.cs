using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Topics.ComplianceTopic;
using Microsoft.Extensions.Logging;
using ConversaCore.Context;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Enhanced compliance topic that chains with CaliforniaResidentDemoTopic.
    /// Adapts TCPA/CCPA messaging based on California residency status.
    /// Routes to 18 possible outcomes based on user responses.
    /// </summary>
    public class ComplianceTopic : TopicFlow
    {
        public const string ActivityId_ShowCard = "ShowComplianceCard";
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_DecisionMatrix = "DecisionMatrix";
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
            Context.SetValue("TopicName", "Enhanced Compliance & Consent");

            // Initialize activities
            InitializeActivities();
        }

        public override void Reset()
        {
            // Clear our specific context keys
            Context.SetValue("ComplianceTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("ComplianceTopic_completed", null);
            Context.SetValue("ComplianceTopic_state", null);
            Context.SetValue("ShowComplianceCard_sent", null);
            Context.SetValue("ShowComplianceCard_rendered", null);
            
            // Call base reset which will reset activities and FSM
            base.Reset();
            
            // Fix any remaining state issues by forcing FSM to Idle
            var stateMachine = GetType().BaseType?.GetField("_fsm", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance)?.GetValue(this);
            
            if (stateMachine is ConversaCore.StateMachine.ITopicStateMachine<TopicFlow.FlowState> fsm) {
                fsm.ForceState(TopicFlow.FlowState.Idle, 
                    "Forced reset to Idle in ComplianceTopic.Reset");
                fsm.ClearTransitionHistory();
                _logger.LogInformation("[ComplianceTopic] State machine forced to Idle state during Reset");
            }
            
            // Re-initialize activities after reset
            InitializeActivities();
            
            // Clear any flags that might prevent proper execution
            Context.SetValue("ShowComplianceCard_Completed", false);
            
            _logger.LogInformation("[ComplianceTopic] Topic reset completed with fresh activity queue");
        }

        private void InitializeActivities()
        {
            // Get California residency status from previous topic (if available)
            var isCaliforniaResident = Context.GetValue<bool?>("is_california_resident") ?? false;
            var zipCode = Context.GetValue<string>("california_zip_code") ?? "";

            var showCardActivity = new AdaptiveCardActivity<ComplianceCard, ComplianceModel>(
                ActivityId_ShowCard,
                Context,
                cardFactory: card => card.Create(isCaliforniaResident, zipCode),
                modelContextKey: "ComplianceModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    Console.WriteLine(
                        $"[ComplianceCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
                    );
                }
            );

            var isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            var dumpCtxActivity = new DumpCtxActivity(ActivityId_DumpCtx, isDevelopment);

            // Decision matrix - route based on collected responses
            var decisionMatrixActivity = new SimpleActivity(ActivityId_DecisionMatrix, (ctx, data) => {
                var tcpaConsent = Context.GetValue<bool?>("tcpa_consent");
                var ccpaAcknowledgment = Context.GetValue<bool?>("ccpa_acknowledgment");
                var outcomeType = DetermineOutcomeType(isCaliforniaResident, tcpaConsent, ccpaAcknowledgment);
                
                Context.SetValue("compliance_outcome", outcomeType);
                Context.SetValue("can_contact_user", tcpaConsent == true);
                
                _logger.LogInformation("[{Topic}] Decision Matrix: CA={CA}, TCPA={TCPA}, CCPA={CCPA} → {Outcome}",
                    Name, isCaliforniaResident, tcpaConsent, ccpaAcknowledgment, outcomeType);
                
                // Set next topic based on outcome
                SetNextTopicBasedOnOutcome(outcomeType);
                
                // Update the trigger activity with the new next topic
                UpdateTriggerActivity();
                
                return Task.FromResult<object?>(null);
            });

            // We'll create the trigger activity dynamically in UpdateTriggerActivity
            // based on the current context values right before triggering
            // This ensures we have the most up-to-date next topic name

            // === Event hooks for AdaptiveCard lifecycle ===
            showCardActivity.CardJsonEmitted += (s, e) =>
                _logger.LogInformation("[{Topic}] Card JSON emitted (mode={Mode})", Name, e.RenderMode);

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

            // === Enqueue activities ===
            Add(showCardActivity);
            Add(decisionMatrixActivity);
            // Trigger activity will be added in UpdateTriggerActivity method
        }

        /// <summary>
        /// Determines the outcome type based on the decision matrix.
        /// Maps to the 18 possible outcomes in the flowchart.
        /// </summary>
        private string DetermineOutcomeType(bool isCaliforniaResident, bool? tcpaConsent, bool? ccpaAcknowledgment)
        {
            var prefix = isCaliforniaResident ? "CA" : "NonCA";
            
            var tcpaStatus = tcpaConsent switch {
                true => "TcpaYes",
                false => "TcpaNo", 
                null => "TcpaUnknown"
            };
            
            var ccpaStatus = ccpaAcknowledgment switch {
                true => "CcpaYes",
                false => "CcpaNo",
                null => "CcpaUnknown"
            };
            
            return $"{prefix}_{tcpaStatus}_{ccpaStatus}";
        }

        /// <summary>
        /// Sets the next topic based on the compliance outcome.
        /// </summary>
        private void SetNextTopicBasedOnOutcome(string outcomeType)
        {
            var canContact = Context.GetValue<bool>("can_contact_user");
            
            var nextTopic = outcomeType switch
            {
                // Full marketing outcomes - continue with lead workflow
                "CA_TcpaYes_CcpaYes" or "NonCA_TcpaYes_CcpaYes" or 
                "NonCA_TcpaYes_CcpaNo" or "NonCA_TcpaYes_CcpaUnknown" => 
                    canContact ? "MarketingTypeOneTopic" : "SelfServiceTopic",
                
                // Limited marketing outcomes - educational content first  
                "CA_TcpaYes_CcpaNo" or "CA_TcpaYes_CcpaUnknown" => 
                    "EducationalContentTopic",
                
                // No marketing outcomes - informational content only
                "CA_TcpaNo_CcpaYes" or "CA_TcpaNo_CcpaUnknown" or "CA_TcpaUnknown_CcpaYes" or
                "NonCA_TcpaNo_CcpaYes" or "NonCA_TcpaNo_CcpaUnknown" or "NonCA_TcpaUnknown_CcpaYes" => 
                    "InformationalContentTopic",
                
                // Blocked/restricted outcomes - minimal interaction
                "CA_TcpaNo_CcpaNo" or "CA_TcpaUnknown_CcpaNo" or "CA_TcpaUnknown_CcpaUnknown" or
                "NonCA_TcpaNo_CcpaNo" or "NonCA_TcpaUnknown_CcpaNo" or "NonCA_TcpaUnknown_CcpaUnknown" => 
                    "BasicNavigationTopic",
                
                _ => "FallbackTopic"
            };
            
            Context.SetValue("NextTopic", nextTopic);
            _logger.LogInformation("[{Topic}] Setting next topic: {NextTopic} based on outcome: {Outcome}", 
                Name, nextTopic, outcomeType);
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
        /// Handles restart scenarios and next topic routing.
        /// </summary>
        public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
        {
            Context.SetValue("ComplianceTopic_runasync", DateTime.UtcNow.ToString("o"));

            // Redefine the trigger activity each time we run
            // This ensures we have the most up-to-date next topic name
            UpdateTriggerActivity();
            
            // Run the topic normally
            return base.RunAsync(cancellationToken);
        }
        
        /// <summary>
        /// Updates the trigger activity with the current next topic name from context
        /// </summary>
        private void UpdateTriggerActivity()
        {
            // Get the current topic name from context
            var nextTopic = Context.GetValue<string>("NextTopic") ?? "FallbackTopic";
            
            // Remove existing trigger activity if present
            RemoveActivity(ActivityId_Trigger);
            
            // Create new trigger activity with current topic name
            var triggerActivity = new TriggerTopicActivity(
                ActivityId_Trigger, 
                nextTopic,
                _logger,
                false, // waitForCompletion
                _conversationContext
            );
            
            // Hook up the trigger event to log the next topic
            triggerActivity.TopicTriggered += (sender, e) =>
            {
                _logger.LogInformation("[{Topic}] Triggering next topic: {Next}", Name, e.TopicName);
                _conversationContext.AddTopicToChain(e.TopicName);
            };
            
            // Add the updated activity
            Add(triggerActivity);
        }
    }
}