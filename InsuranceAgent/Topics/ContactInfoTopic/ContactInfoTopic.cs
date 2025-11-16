using ConversaCore.Models;
using ConversaCore.TopicFlow;

namespace InsuranceAgent.Topics;

/// <summary>
/// Topic for collecting contact information and preferences.
/// Ported from Copilot Studio adaptive card.
/// Event-driven, queue-based flow of activities.
/// </summary>
public class ContactInfoTopic : TopicFlow
{
    public const string ActivityId_ShowCard = "ShowContactInfoCard";
    public const string ActivityId_DumpCtx = "DumpCTX";
    public const string ActivityId_Trigger = "TriggerNextTopic";

    /// <summary>
    /// Keywords for topic routing.
    /// </summary>
    public static readonly string[] IntentKeywords = new[] {
        "contact", "contact info", "contact information", "name", "phone", "email",
        "phone number", "email address", "full name", "contact me", "reach me",
        "contact preference", "contact method", "contact time", "morning", "afternoon",
        "evening", "anytime", "best time", "consent", "agree to contact"
    };

    private readonly ConversaCore.Context.IConversationContext _conversationContext;
    private readonly ILogger<ContactInfoTopic> _logger;

    public ContactInfoTopic(
        TopicWorkflowContext context,
        ILogger<ContactInfoTopic> logger,
        ConversaCore.Context.IConversationContext conversationContext
    ) : base(context, logger, name: "ContactInfoTopic")
    {
        _logger = logger;
        _conversationContext = conversationContext;

        Context.SetValue("ContactInfoTopic_create", DateTime.UtcNow.ToString("o"));
        Context.SetValue("TopicName", "Contact Information");

        // === Activities in queue order ===
        var showCardActivity = new AdaptiveCardActivity<ContactInfoCard, ContactInfoModel>(
            ActivityId_ShowCard,
            context,
            cardFactory: card => card.Create(),
            modelContextKey: "ContactInfoModel",
            onTransition: (from, to, data) => {
                var stamp = DateTime.UtcNow.ToString("o");
                Console.WriteLine(
                    $"[ContactInfoCardActivity] {ActivityId_ShowCard}: {from} → {to} @ {stamp} | Data={data?.GetType().Name ?? "null"}"
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
            
            // Store contact information in conversation context for other topics to access
            if (e.Model is ContactInfoModel contactInfoModel)
            {
                Context.SetValue("contact_info_data", contactInfoModel);
                Context.SetValue("lead_name", contactInfoModel.FullName);
                Context.SetValue("lead_phone", contactInfoModel.PhoneNumber);
                Context.SetValue("lead_email", contactInfoModel.EmailAddress);
                Context.SetValue("best_contact_time", contactInfoModel.BestContactTime);
                Context.SetValue("preferred_contact_method", contactInfoModel.PreferredContactMethod);
                Context.SetValue("contact_consent", contactInfoModel.HasContactConsent);
                Context.SetValue("lead_quality_score", contactInfoModel.LeadQualityScore);
                Context.SetValue("lead_quality_grade", contactInfoModel.LeadQualityGrade);
                
                _logger.LogInformation("[{Topic}] Contact Info - Name: {Name}, Quality: {Grade} ({Score}), Consent: {Consent}", 
                    Name, contactInfoModel.FullName, contactInfoModel.LeadQualityGrade, 
                    contactInfoModel.LeadQualityScore, contactInfoModel.HasContactConsent);
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
    /// Intent detection (keyword matching for contact information topics).
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
        Context.SetValue("ContactInfoTopic_runasync", DateTime.UtcNow.ToString("o"));

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