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

            // Surface the reset message and continue
            return Task.FromResult(ActivityResult.Continue(_message));
        }
    }
}
