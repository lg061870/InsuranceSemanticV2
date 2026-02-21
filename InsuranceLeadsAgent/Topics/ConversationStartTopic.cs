using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;

namespace InsuranceLeadsAgent.Topics
{
    public class ConversationStartTopic : TopicFlow
    {
        private readonly ILogger<ConversationStartTopic> _logger;

        public ConversationStartTopic(
            TopicWorkflowContext context,
            ILogger<ConversationStartTopic> logger)
            : base(context, logger, "ConversationStart")
        {
            _logger = logger;
            BuildWorkflow();
        }

        public override void Reset()
        {
            base.Reset();
            BuildWorkflow();
        }

        private void BuildWorkflow()
        {
            Add(new TriggerTopicActivity(
                "StartLeadOrchestrator",
                "LeadOrchestrator",
                waitForCompletion: true,
                logger: _logger
            ));

            Add(new EndActivity("ConversationStartEnd"));
        }
    }
}
