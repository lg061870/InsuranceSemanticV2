using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ConversaCore.TopicFlow {
    public class TopicTriggeredEventArgs : EventArgs {
        public string TopicName { get; }
        public TopicTriggeredEventArgs(string topicName) {
            TopicName = topicName;
        }
    }

    /// <summary>
    /// An activity that signals the orchestrator to start another topic.
    /// It does not run the topic directly — it just emits the trigger.
    /// </summary>
    public class TriggerTopicActivity : TopicFlowActivity {
        private readonly ILogger? _logger;

        public string TopicToTrigger { get; }

        public event EventHandler<TopicTriggeredEventArgs>? TopicTriggered;

        public TriggerTopicActivity(string id, string topicToTrigger, ILogger? logger = null)
            : base(id) {
            TopicToTrigger = topicToTrigger
                ?? throw new ArgumentNullException(nameof(topicToTrigger));
            _logger = logger;
        }

        protected override Task<ActivityResult> RunActivity(
            TopicWorkflowContext context,
            object? input = null,
            CancellationToken cancellationToken = default) {
            _logger?.LogInformation("[TriggerTopicActivity] Running {ActivityId}", Id);
            _logger?.LogInformation("[TriggerTopicActivity] About to trigger topic: {Topic}", TopicToTrigger);

            // Raise event for orchestrator
            TopicTriggered?.Invoke(this, new TopicTriggeredEventArgs(TopicToTrigger));
            _logger?.LogInformation("[TriggerTopicActivity] Event TopicTriggered raised for {Topic}", TopicToTrigger);

            // Set marker in context (optional: for chaining logic)
            context.SetValue("NextTopic", TopicToTrigger);
            _logger?.LogDebug("[TriggerTopicActivity] Context marker set: NextTopic={Topic}", TopicToTrigger);

            // Mark this activity as completed
            var payload = new { Type = "Trigger", Topic = TopicToTrigger };
            _logger?.LogInformation("[TriggerTopicActivity] Completed with payload: {@Payload}", payload);

            return Task.FromResult(ActivityResult.End(payload));
        }
    }
}
