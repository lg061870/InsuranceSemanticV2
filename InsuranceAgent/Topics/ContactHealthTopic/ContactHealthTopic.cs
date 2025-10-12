using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Topics.ContactHealthTopic;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Topics.ContactHealthTopic
{
    /// <summary>
    /// Topic for collecting contact and health details.
    /// Ported from Copilot Studio adaptive card.
    /// Event-driven, queue-based flow of activities.
    /// </summary>
    public class ContactHealthTopic : TopicFlow
    {
        public const string ActivityId_ShowCard = "ShowContactHealthCard";
        public const string ActivityId_DumpCtx = "DumpCTX";
        public const string ActivityId_Trigger = "TriggerNextTopic";

        /// <summary>
        /// Keywords for topic routing.
        /// </summary>
        public static readonly string[] IntentKeywords = new[] {
            "contact", "address", "health", "medical", "medication", "hospitalized",
            "tobacco", "smoking", "date of birth", "dob", "city", "state",
            "health details", "medical history", "health questions"
        };

        private readonly ConversaCore.Context.IConversationContext _conversationContext;
        private readonly ILogger<ContactHealthTopic> _logger;

        public ContactHealthTopic(
            TopicWorkflowContext context,
            ILogger<ContactHealthTopic> logger,
            ConversaCore.Context.IConversationContext conversationContext
        ) : base(context, logger, name: "ContactHealthTopic")
        {
            _logger = logger;
            _conversationContext = conversationContext;

            Context.SetValue("ContactHealthTopic_create", DateTime.UtcNow.ToString("o"));
            Context.SetValue("TopicName", "Contact & Health Details");

            // === Activities in queue order ===
            var showCardActivity = new AdaptiveCardActivity<ContactHealthCard, ContactHealthModel>(
                ActivityId_ShowCard,
                context,
                cardFactory: card => card.Create(),
                modelContextKey: "ContactHealthModel",
                onTransition: (from, to, data) => {
                    var stamp = DateTime.UtcNow.ToString("o");
                    Console.WriteLine(
                        $"[ContactHealthCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
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
                
                // Store contact and health data in conversation context for other topics to access
                if (e.Model is ContactHealthModel contactHealthModel)
                {
                    Context.SetValue("contact_health_data", contactHealthModel);
                    Context.SetValue("customer_address", contactHealthModel.Address);
                    Context.SetValue("customer_city_state", contactHealthModel.CityState);
                    Context.SetValue("customer_dob", contactHealthModel.DateOfBirth);
                    Context.SetValue("customer_age", contactHealthModel.CalculatedAge);
                    Context.SetValue("has_health_risks", contactHealthModel.HasHealthRiskFactors);
                    
                    _logger.LogInformation("[{Topic}] Contact & Health - Age: {Age}, Health Risks: {HasRisks}", 
                        Name, contactHealthModel.CalculatedAge, contactHealthModel.HasHealthRiskFactors);
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
        /// Intent detection (keyword matching for contact/health topics).
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
            Context.SetValue("ContactHealthTopic_runasync", DateTime.UtcNow.ToString("o"));

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