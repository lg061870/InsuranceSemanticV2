using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.TopicFlow.Activities {
    /// <summary>
    /// Clears conversation state and emits a reset message.
    /// </summary>
    public class ResetActivity : TopicFlowActivity {
        private readonly string _message;

        public ResetActivity(string id, string message) : base(id) {
            _message = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <inheritdoc/>
        protected override Task<ActivityResult> RunActivity(
            TopicWorkflowContext context,
            object? input = null,
            CancellationToken cancellationToken = default) {
            // Reset core context values
            context.SetValue("Messages", new List<(string Role, string Content)>());
            context.SetValue("TopicChain", new Queue<string>());
            context.SetValue("CurrentTopic", string.Empty);
            
            // Reset any activity-specific flags that might prevent cards from displaying
            context.SetValue("ShowComplianceCard_Rendered", false);
            context.SetValue("ShowComplianceCard_Sent", false);
            context.SetValue("ComplianceTopicState", null);
            
            // Clear all completion flags for topics
            foreach (var key in context.GetKeys().Where(k => k.EndsWith("_Completed") || k.EndsWith("State")))
            {
                context.SetValue(key, null);
            }

            // Surface the reset message and continue
            return Task.FromResult(ActivityResult.Continue(_message));
        }
    }
}
