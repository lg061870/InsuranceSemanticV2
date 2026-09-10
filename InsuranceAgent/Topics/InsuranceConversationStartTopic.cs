using ConversaCore.Cards;
using ConversaCore.Context;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using InsuranceAgent.Cards;
using InsuranceAgent.Topics;
using CoreContextExtensions = ConversaCore.TopicFlow.Core.TopicWorkflowContextExtensions;

namespace InsuranceAgent.Topics;

/// <summary>
/// InsuranceAgent's explicit start composition. The framework activates this topic as a
/// normal scoped topic; it does not require a domain agent to mutate a system topic.
/// </summary>
public sealed class InsuranceConversationStartTopic : TopicFlow
{
    private readonly ILogger<InsuranceConversationStartTopic> _logger;
    private readonly IConversationContext _conversationContext;

    public InsuranceConversationStartTopic(
        TopicWorkflowContext context,
        ILogger<InsuranceConversationStartTopic> logger,
        IConversationContext conversationContext)
        : base(context, logger, InsuranceTopicIds.ConversationStart)
    {
        _logger = logger;
        _conversationContext = conversationContext;
        BuildWorkflow();
    }

    public override int Priority => int.MaxValue;

    public override Task<float> CanHandleAsync(
        string message,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(string.IsNullOrEmpty(message) ? 1f : 0f);

    private void BuildWorkflow()
    {
        Add(new GreetingActivity("insurance.greet"));
        Add(new TriggerTopicActivity(
            "insurance.collect-compliance",
            "ComplianceTopic",
            _logger,
            waitForCompletion: true,
            conversationContext: _conversationContext));
        Add(new SimpleActivity("insurance.process-compliance", (_, _) =>
            Task.FromResult<object?>(null)));

        Add(FlowConditionHelpers.IfCase(
            "insurance.tcpa-yes",
            context => CoreContextExtensions.IsYes(context, "tcpa_consent"),
            ConditionalActivity<TopicFlowActivity>.If(
                "insurance.ca-check",
                context => CoreContextExtensions.IsYes(context, "is_california_resident"),
                (_, workflowContext) => new CompositeActivity(
                    "insurance.ccpa-check",
                    [
                        CreateCaliforniaResidencyCard(workflowContext),
                        ConditionalActivity<TopicFlowActivity>.If(
                            "insurance.ccpa-acknowledged",
                            context => CoreContextExtensions.IsYes(context, "ccpa_acknowledgment"),
                            (_, _) => Marketing(InsuranceTopicIds.MarketingT1, "insurance.after-ccpa-yes"),
                            (_, _) => Marketing(InsuranceTopicIds.MarketingT2, "insurance.after-ccpa-no"))
                    ]),
                (_, _) => Marketing(InsuranceTopicIds.MarketingT1, "insurance.non-ca-tcpa-yes"))
            ));
        Add(FlowConditionHelpers.IfCase(
            "insurance.tcpa-no",
            context => CoreContextExtensions.IsNo(context, "tcpa_consent"),
            new TriggerTopicActivity(
                "insurance.tcpa-no",
                InsuranceTopicIds.MarketingT3,
                _logger,
                waitForCompletion: false,
                conversationContext: _conversationContext)));
    }

    private TopicFlowActivity Marketing(string topicId, string activityId) =>
        new TriggerTopicActivity(
            activityId,
            topicId,
            _logger,
            waitForCompletion: false,
            conversationContext: _conversationContext);

    private TopicFlowActivity CreateCaliforniaResidencyCard(TopicWorkflowContext context) =>
        new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
            "insurance.ca-residency",
            context,
            cardFactory: card => card.Create(
                isResident: true,
                zip_code: context.GetValue<string>("zip_code"),
                ccpa_acknowledgment: context.GetValue<string?>("ccpa_acknoledgement")));
}
