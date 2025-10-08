using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConversaCore.Context;

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
    /// Can optionally wait for the triggered topic to complete before continuing.
    /// </summary>
    public class TriggerTopicActivity : TopicFlowActivity {
        private readonly ILogger? _logger;
        private readonly IConversationContext? _conversationContext;

        public string TopicToTrigger { get; }
        
        /// <summary>
        /// If true, this activity will wait for the triggered topic to complete
        /// before allowing the calling topic to continue with its next activity.
        /// If false, uses the legacy hand-off behavior (calling topic ends).
        /// </summary>
        public bool WaitForCompletion { get; }

        public event EventHandler<TopicTriggeredEventArgs>? TopicTriggered;

        public TriggerTopicActivity(string id, string topicToTrigger, ILogger? logger = null, bool waitForCompletion = false, IConversationContext? conversationContext = null)
            : base(id) {
            TopicToTrigger = topicToTrigger
                ?? throw new ArgumentNullException(nameof(topicToTrigger));
            _logger = logger;
            WaitForCompletion = waitForCompletion;
            _conversationContext = conversationContext;
        }

        protected override Task<ActivityResult> RunActivity(
            TopicWorkflowContext context,
            object? input = null,
            CancellationToken cancellationToken = default) {
            _logger?.LogInformation("[TriggerTopicActivity] Running {ActivityId} (WaitForCompletion: {WaitForCompletion})", Id, WaitForCompletion);
            _logger?.LogInformation("[TriggerTopicActivity] About to trigger topic: {Topic}", TopicToTrigger);

            // Check for cycle detection if we have conversation context
            if (WaitForCompletion && _conversationContext != null) {
                if (_conversationContext.IsTopicInCallStack(TopicToTrigger)) {
                    _logger?.LogWarning("[TriggerTopicActivity] Circular topic call detected: {Topic} is already in call stack", TopicToTrigger);
                    return Task.FromResult(ActivityResult.Continue("Circular topic call prevented"));
                }

                // Get the current topic name for stack tracking
                var currentTopic = _conversationContext.CurrentTopicName ?? "Unknown";
                _logger?.LogDebug("[TriggerTopicActivity] Pushing topic call: {CallingTopic} -> {SubTopic}", currentTopic, TopicToTrigger);
                
                // Push the call onto the stack with any resume data
                _conversationContext.PushTopicCall(currentTopic, TopicToTrigger, input);
            }

            // Raise event for orchestrator
            TopicTriggered?.Invoke(this, new TopicTriggeredEventArgs(TopicToTrigger));
            _logger?.LogInformation("[TriggerTopicActivity] Event TopicTriggered raised for {Topic}", TopicToTrigger);

            // Set marker in context (for legacy compatibility)
            context.SetValue("NextTopic", TopicToTrigger);
            _logger?.LogDebug("[TriggerTopicActivity] Context marker set: NextTopic={Topic}", TopicToTrigger);

            if (WaitForCompletion) {
                // New behavior: Wait for sub-topic to complete, then resume
                _logger?.LogInformation("[TriggerTopicActivity] Waiting for sub-topic '{Topic}' to complete (Call depth: {Depth})", 
                    TopicToTrigger, _conversationContext?.GetTopicCallDepth() ?? 0);
                return Task.FromResult(ActivityResult.WaitForSubTopic(TopicToTrigger));
            } else {
                // Legacy behavior: Hand off control and end this topic
                var payload = new { Type = "Trigger", Topic = TopicToTrigger };
                _logger?.LogInformation("[TriggerTopicActivity] Legacy hand-off completed with payload: {@Payload}", payload);
                return Task.FromResult(ActivityResult.End(payload));
            }
        }
    }
}
