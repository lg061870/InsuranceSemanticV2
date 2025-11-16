using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Cards;
using ConversaCore.Context;

namespace InsuranceAgent.Topics;

/// <summary>
/// Collects CCPA acknowledgment for California residents.
/// Saves result into context; no topic triggering here.
/// </summary>
public class CaliforniaResidentTopic : TopicFlow {
    public const string ActivityId_ShowCard = "ShowCaliforniaResidentCard";
    public const string ActivityId_TriggerConsole = "TriggerCustomerConsole";

    private readonly ILogger<CaliforniaResidentTopic> _logger;
    private readonly IConversationContext? _conversationContext;
    private static int _constructorCallCount = 0;

    public CaliforniaResidentTopic(
        TopicWorkflowContext context,
        ILogger<CaliforniaResidentTopic> logger,
        IConversationContext? conversationContext = null)
        : base(context, logger, name: "CaliforniaResidentTopic") {
        _logger = logger;
        _conversationContext = conversationContext;

        _logger.LogInformation("[CaliforniaResidentTopic] Constructor #{Count} @ {Time}",
            ++_constructorCallCount, DateTime.UtcNow);

        // ───────────────────────────────
        // Adaptive Card
        // ───────────────────────────────
        var showCardActivity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
            ActivityId_ShowCard,
            context,
            cardFactory: card => card.Create(zip_code: context.GetValue<string>("zip_code")),
            modelContextKey: "CaliforniaResidentModel",
            onTransition: (from, to, data) => {
                if (to == ActivityState.Completed && data is CaliforniaResidentModel model) {
                    Context.SetValue("ccpa_acknowledgment", model.HasCcpaAcknowledgment);
                    Context.SetValue("is_california_resident", true);
                    _logger.LogInformation(
                        "[{Topic}] Saved CCPA={CCPA} (CA resident assumed true)",
                        Name, model.HasCcpaAcknowledgment);
                }
            }
        ) {
            IsRequired = true   // ✅ required card
        };

        showCardActivity.ValidationFailed += (s, e) =>
            _logger.LogWarning("[{Topic}] Validation failed: {Message}", Name, e.Exception.Message);

        Add(showCardActivity);
    }
}
