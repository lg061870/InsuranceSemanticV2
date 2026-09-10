using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;

namespace InsuranceAgent.Topics.MarketingTypeTopics;

/// <summary>Handles the no-marketing-consent path without collecting or persisting lead data.</summary>
public sealed class MarketingT3Topic : TopicFlow
{
    public MarketingT3Topic(
        TopicWorkflowContext context,
        ILogger<MarketingT3Topic> logger)
        : base(context, logger, InsuranceTopicIds.MarketingT3)
    {
        Context.SetValue("marketing_path_type", "T3");
        Add(new SimpleActivity(
            "insurance.marketing.t3.explain",
            "We respect your choice. No marketing lead or profile data will be created. You can restart the conversation if you want to review consent options."));
    }

    public override Task<float> CanHandleAsync(
        string message,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0f);
}
