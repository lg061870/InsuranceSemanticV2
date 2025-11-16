using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Context;
using ConversaCore.Interfaces;

namespace InsuranceAgent.Topics;

/// <summary>
/// Updated ComplianceTopic — collects TCPA consent and ZIP code,
/// determines California residency, and optionally triggers CaliforniaResidentTopic.
/// </summary>
public class ComplianceTopic : TopicFlow, ITopicTriggeredActivity {
    public const string ActivityId_ShowCard = "ShowComplianceCard";

    public event EventHandler<TopicTriggeredEventArgs>? TopicTriggered;

    public bool WaitForCompletion => false;

    public static readonly string[] IntentKeywords = new[]
    {
        "consent", "compliance", "tcpa", "ccpa", "privacy", "contact permission",
        "legal consent", "agreement", "terms", "privacy notice", "zip"
    };

    private readonly IConversationContext _conversationContext;
    private readonly ILogger<ComplianceTopic> _logger;

    public ComplianceTopic(
        TopicWorkflowContext context,
        ILogger<ComplianceTopic> logger,
        IConversationContext conversationContext)
        : base(context, logger, name: "ComplianceTopic") {
        _logger = logger;
        _conversationContext = conversationContext;

        Context.SetValue("ComplianceTopic_create", DateTime.UtcNow.ToString("o"));
        Context.SetValue("TopicName", "TCPA & ZIP Compliance");

        InitializeActivities();
    }

    public override void Reset() {
        Context.SetValue("ComplianceTopic_create", DateTime.UtcNow.ToString("o"));
        Context.SetValue("ComplianceTopic_completed", null);
        Context.SetValue("ComplianceTopic_state", null);
        Context.SetValue("ShowComplianceCard_sent", null);
        Context.SetValue("ShowComplianceCard_rendered", null);

        base.Reset();

        // Force FSM idle for safety
        var stateMachine = GetType().BaseType?.GetField("_fsm",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance)?.GetValue(this);

        if (stateMachine is ConversaCore.StateMachine.ITopicStateMachine<TopicFlow.FlowState> fsm) {
            fsm.ForceState(TopicFlow.FlowState.Idle, "Forced reset to Idle in ComplianceTopic.Reset");
            fsm.ClearTransitionHistory();
            _logger.LogInformation("[ComplianceTopic] FSM reset to Idle.");
        }

        InitializeActivities();
    }

    private void InitializeActivities() {
        // Pre-fill any known ZIP from context
        var zipCode = Context.GetValue<string>("zip_code") ?? "";

        var showCardActivity = new AdaptiveCardActivity<ComplianceCard, ComplianceModel>(
            ActivityId_ShowCard,
            Context,
            cardFactory: card => card.Create(zipCode),
            modelContextKey: "ComplianceModel",
            onTransition: (from, to, data) => {
                _logger.LogInformation($"[ComplianceTopic] {ActivityId_ShowCard}: {from} → {to}");
            }
        ) {
            IsRequired = true   // ✅ mark this card as required
        };


        // === Event hooks ===
        showCardActivity.CardJsonEmitted += (s, e) =>
            _logger.LogInformation("[{Topic}] Card JSON emitted (mode={Mode})", Name, e.RenderMode);

        showCardActivity.ModelBound += (s, e) => {
            if (e.Model is ComplianceModel model) {
                Context.SetValue("is_california_resident", model.IsCaliforniaZip);

                _logger.LogInformation(
                    "[{Topic}] Bound ComplianceModel — TCPA: {Tcpa}, ZIP: {Zip}, IsCA: {CA}",
                    Name, model.HasTcpaConsent, model.ZipCode, model.IsCaliforniaZip
                );
            }
        };

        showCardActivity.ValidationFailed += (s, e) =>
            _logger.LogWarning("[{Topic}] Validation failed: {Message}", Name, e.Exception.Message);

        Add(showCardActivity);
    }

    public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(message)) return Task.FromResult(0f);
        var msg = message.ToLowerInvariant();

        var matchCount = IntentKeywords.Count(kw => msg.Contains(kw));
        var confidence = matchCount > 0 ? Math.Min(1.0f, matchCount / 3.0f) : 0f;

        _logger.LogDebug("[{Topic}] Intent confidence {Confidence} for: {Message}", Name, confidence, message);
        return Task.FromResult(confidence);
    }
}
