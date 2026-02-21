using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;

namespace InsuranceLeadsAgent.Topics
{
    public class IntakeTopic : TopicFlow
    {
        private readonly ILogger<IntakeTopic> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public IntakeTopic(
            TopicWorkflowContext context,
            ILogger<IntakeTopic> logger,
            ILoggerFactory loggerFactory)
            : base(context, logger, "Intake")
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            BuildWorkflow();
        }

        public override void Reset()
        {
            base.Reset();
            BuildWorkflow();
        }

        private void BuildWorkflow()
        {
            Add(new SimpleActivity(
                "CheckShouldProceed",
                (ctx, _) =>
                {
                    var shouldProceed = ctx.GetValue<bool>("should_proceed_to_intake");
                    if (!shouldProceed)
                    {
                        // User chose not to proceed, exit topic immediately
                        return Task.FromResult<object?>(null);
                    }
                    
                    return Task.FromResult<object?>("Starting intake process...");
                }
            ));

            Add(new QuickAnswerActivity(
                "ContactMethodSelection",
                "How would you prefer to continue?",
                new[]
                {
                    "Phone call",
                    "Email",
                    "Both"
                },
                Context,
                _loggerFactory.CreateLogger<QuickAnswerActivity>()
            ));

            // Intake form placeholder - will be replaced with actual card later
            Add(new SimpleActivity(
                "LeadIntakeFormPlaceholder",
                "Thank you. I've recorded your contact preference."
            ));

            Add(new SimpleActivity(
                "MarkIntakeComplete",
                (ctx, _) =>
                {
                    ctx.SetValue("intake_completed", true);
                    return Task.FromResult<object?>(null);
                }
            ));

            Add(new CompleteTopicActivity("IntakeComplete"));
        }
    }
}
