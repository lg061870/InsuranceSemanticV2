using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Topics.LeadDetailsTopic;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Topics.LeadDetailsTopic
{
    /// <summary>
    /// Topic for collecting lead management and sales tracking information.
    /// Ported from Copilot Studio adaptive card.
    /// Event-driven, queue-based flow of activities.
    /// </summary>
    public class LeadDetailsTopic : TopicFlow
    {
        public const string ActivityId_ShowCard = "ShowLeadDetailsCard";
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_Trigger = "TriggerNextTopic";

        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] {
            "lead details", "lead name", "lead source", "interest level", "lead intent",
            "appointment", "follow up", "sales agent", "notes", "lead url",
            "language", "preferred language", "referral", "website", "phone",
            "high interest", "medium interest", "low interest", "buy", "learn", "compare",
            "schedule", "callback", "urgent", "priority", "lead management"
        };

        private readonly ConversaCore.Context.IConversationContext _conversationContext;
        private readonly ILogger<LeadDetailsTopic> _logger;

        public LeadDetailsTopic(
            TopicWorkflowContext context,
            ILogger<LeadDetailsTopic> logger,
            ConversaCore.Context.IConversationContext conversationContext
        ) : base(context, logger, name: "LeadDetailsTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;

            Context.SetValue("LeadDetailsTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("TopicName", "Lead Details");

            // === Activities in queue order ===
            var showCardActivity = new AdaptiveCardActivity<LeadDetailsCard, LeadDetailsModel>(
                ActivityId_ShowCard,
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "LeadDetailsModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    Console.WriteLine(
                        $"[LeadDetailsCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
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
                
                // Store lead details data in conversation context for CRM integration and sales process
                if (e.Model is LeadDetailsModel leadDetailsModel)
                {
                    Context.SetValue("lead_details_data", leadDetailsModel);
                    Context.SetValue("lead_name", leadDetailsModel.LeadName);
                    Context.SetValue("language", leadDetailsModel.Language);
                    Context.SetValue("normalized_language", leadDetailsModel.NormalizedLanguage);
                    Context.SetValue("requires_specialized_support", leadDetailsModel.RequiresSpecializedSupport);
                    Context.SetValue("lead_source", leadDetailsModel.LeadSource);
                    Context.SetValue("normalized_lead_source", leadDetailsModel.NormalizedLeadSource);
                    Context.SetValue("lead_source_quality_score", leadDetailsModel.LeadSourceQualityScore);
                    Context.SetValue("interest_level", leadDetailsModel.InterestLevel);
                    Context.SetValue("normalized_interest_level", leadDetailsModel.NormalizedInterestLevel);
                    Context.SetValue("interest_level_score", leadDetailsModel.InterestLevelScore);
                    Context.SetValue("lead_intent", leadDetailsModel.LeadIntent);
                    Context.SetValue("normalized_lead_intent", leadDetailsModel.NormalizedLeadIntent);
                    Context.SetValue("lead_intent_score", leadDetailsModel.LeadIntentScore);
                    Context.SetValue("appointment_date_time", leadDetailsModel.AppointmentDateTime);
                    Context.SetValue("parsed_appointment_date_time", leadDetailsModel.ParsedAppointmentDateTime);
                    Context.SetValue("has_valid_appointment", leadDetailsModel.HasValidAppointment);
                    Context.SetValue("is_appointment_today", leadDetailsModel.IsAppointmentToday);
                    Context.SetValue("is_appointment_this_week", leadDetailsModel.IsAppointmentThisWeek);
                    Context.SetValue("follow_up_needed", leadDetailsModel.FollowUpNeeded);
                    Context.SetValue("notes_for_sales_agent", leadDetailsModel.NotesForSalesAgent);
                    Context.SetValue("lead_url", leadDetailsModel.LeadUrl);
                    Context.SetValue("has_valid_lead_url", leadDetailsModel.HasValidLeadUrl);
                    Context.SetValue("lead_details_data_quality_score", leadDetailsModel.LeadDetailsDataQualityScore);
                    Context.SetValue("lead_details_data_quality_grade", leadDetailsModel.LeadDetailsDataQualityGrade);
                    Context.SetValue("lead_qualification_score", leadDetailsModel.LeadQualificationScore);
                    Context.SetValue("lead_qualification_grade", leadDetailsModel.LeadQualificationGrade);
                    Context.SetValue("sales_priority_level", leadDetailsModel.SalesPriorityLevel);
                    Context.SetValue("recommended_actions", leadDetailsModel.RecommendedActions);
                    Context.SetValue("lead_insights", leadDetailsModel.LeadInsights);
                    
                    _logger.LogInformation("[{Topic}] Lead Details - Name: {Name}, Intent: {Intent}, Priority: {Priority} (Score: {Score}), Source: {Source}", 
                        Name, leadDetailsModel.LeadName, leadDetailsModel.NormalizedLeadIntent, 
                        leadDetailsModel.SalesPriorityLevel, leadDetailsModel.LeadQualificationScore, leadDetailsModel.NormalizedLeadSource);
                        
                    // Log urgent/priority situations
                    if (leadDetailsModel.IsAppointmentToday)
                    {
                        _logger.LogWarning("[{Topic}] 🚨 URGENT: Lead {Name} has appointment TODAY at {Time}", 
                            Name, leadDetailsModel.LeadName, leadDetailsModel.ParsedAppointmentDateTime);
                    }
                    else if (leadDetailsModel.IsAppointmentThisWeek)
                    {
                        _logger.LogInformation("[{Topic}] ⏰ Priority: Lead {Name} has appointment this week at {Time}", 
                            Name, leadDetailsModel.LeadName, leadDetailsModel.ParsedAppointmentDateTime);
                    }
                    
                    // Log language requirements
                    if (leadDetailsModel.RequiresSpecializedSupport)
                    {
                        _logger.LogInformation("[{Topic}] 🌍 Language Support Required: {Name} needs {Language}-speaking agent", 
                            Name, leadDetailsModel.LeadName, leadDetailsModel.NormalizedLanguage);
                    }
                    
                    // Log high-value opportunities
                    if (leadDetailsModel.NormalizedLeadIntent == "Buy")
                    {
                        _logger.LogInformation("[{Topic}] 💰 Ready to Buy: {Name} is {Interest} interest, ready to purchase", 
                            Name, leadDetailsModel.LeadName, leadDetailsModel.NormalizedInterestLevel);
                    }
                    
                    if (leadDetailsModel.NormalizedLeadSource == "Referral")
                    {
                        _logger.LogInformation("[{Topic}] ⭐ Referral Lead: {Name} came from referral source - high priority", 
                            Name, leadDetailsModel.LeadName);
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
        /// Intent detection (keyword matching for lead management topics).
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
            Context.SetValue("LeadDetailsTopic_runasync", DateTime.UtcNow.ToString("o"));

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