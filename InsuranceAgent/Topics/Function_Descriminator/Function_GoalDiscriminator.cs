using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.TopicFlow;

namespace InsuranceAgent.Topics {
    /// <summary>
    /// Determines which specialized intent topic to start based on the
    /// user's selected life goals from LifeGoalsCard.
    /// </summary>
    public class Function_GoalDiscriminator : TopicFlow {
        private readonly ILogger<Function_GoalDiscriminator> _logger;
        private readonly IConversationContext _context;

        public Function_GoalDiscriminator(
            TopicWorkflowContext ctx,
            ILogger<Function_GoalDiscriminator> logger,
            IConversationContext conversationContext)
            : base(ctx, logger, name: "function_goalDiscriminator") {
            _logger = logger;
            _context = conversationContext;

            Context.SetValue("TopicName", "Life Goal Discriminator");

            Add(new SimpleActivity("DetermineGoalPath", (c, _) => {
                var selectedGoals = c.GetValue<List<string>>("selected_goals") ?? new();

                string nextTopic = "InformationalContentTopic"; // default fallback
                string reason = "No goals selected";

                if (selectedGoals.Any()) {
                    if (selectedGoals.Contains("ProtectLovedOnes")) {
                        nextTopic = "intent_protectYourLoveOnes";
                        reason = "User wants to protect loved ones";
                    }
                    else if (selectedGoals.Contains("PayMortgage")) {
                        nextTopic = "intent_payOffMortgage";
                        reason = "User wants to cover mortgage";
                    }
                    else if (selectedGoals.Contains("PrepareFuture")) {
                        nextTopic = "intent_prepareForFamilyFuture";
                        reason = "User wants to prepare for family future";
                    }
                    else if (selectedGoals.Contains("CoverExpenses")) {
                        nextTopic = "intent_coverFinalExpenses";
                        reason = "User wants to cover final expenses";
                    }
                    else if (selectedGoals.Contains("PeaceOfMind")) {
                        nextTopic = "intent_peaceOfMind";
                        reason = "User seeks general peace of mind";
                    }
                    else if (selectedGoals.Contains("Unsure")) {
                        nextTopic = "intent_unsureGuidance";
                        reason = "User is unsure — send to guidance flow";
                    }
                }

                // Save routing info
                c.SetValue("goal_discriminator_reason", reason);
                c.SetValue("goal_discriminator_next_topic", nextTopic);

                _logger.LogInformation(
                    "[GoalDiscriminator] {Reason} → Routing to {NextTopic}",
                    reason, nextTopic);

                return Task.FromResult<object?>(null);
            }));
        }

        public override async Task<TopicResult> RunAsync(
            CancellationToken cancellationToken = default) {
            var result = await base.RunAsync(cancellationToken);
            result.NextTopicName = Context.GetValue<string>("goal_discriminator_next_topic");
            return result;
        }
    }
}
