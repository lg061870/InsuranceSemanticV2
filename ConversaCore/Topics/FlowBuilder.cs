using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;

namespace ConversaCore.Topics {
    /// <summary>
    /// Builder for creating and configuring TopicFlow instances using TopicFlowActivity subclasses.
    /// Queue-driven: activities run in the order they are added.
    /// </summary>
    public class FlowBuilder {
        private readonly string _flowName;
        private readonly TopicWorkflowContext _context;
        private readonly TopicFlow.TopicFlow _flow;
        private readonly Dictionary<string, TopicFlowActivity> _activities = new();
        private readonly ILogger _logger;

        private string? _startActivityId;

        public FlowBuilder(string flowName, TopicWorkflowContext context, ILogger logger) {
            _flowName = flowName ?? throw new ArgumentNullException(nameof(flowName));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _flow = new TopicFlow.TopicFlow(context, logger, flowName);
        }

        // -------------------
        // Activity Creation
        // -------------------

        public ActivityBuilder AddSimpleActivity(string id, Func<TopicWorkflowContext, object?> func) {
            var activity = SimpleActivity.Create(id, func);
            AddToFlow(activity);
            return new ActivityBuilder(this, id);
        }

        public ActivityBuilder AddSimpleMessageActivity(string id, string message) {
            var activity = new SimpleActivity(id, message);
            AddToFlow(activity);
            return new ActivityBuilder(this, id);
        }

        public ActivityBuilder AddInteractiveActivity(string id, string message) {
            var activity = InteractiveActivity.Create(id, message);
            AddToFlow(activity);
            return new ActivityBuilder(this, id);
        }

        public ActivityBuilder AddInteractiveActivity(
            string id,
            Func<TopicWorkflowContext, object?, Task<object?>> interaction) {
            // Interaction signature simplified: only returns payload, TopicFlow handles sequencing
            var activity = new InteractiveActivity(id, async (ctx, input) => {
                var payload = await interaction(ctx, input);
                return (payload, (string?)null); // null = let FSM decide next
            });

            AddToFlow(activity);
            return new ActivityBuilder(this, id);
        }

        public ConditionalActivityBuilder AddConditionalActivity(string id, Func<TopicWorkflowContext, string> condition) {
            var activity = ConditionalActivity.Create(id, condition);
            AddToFlow(activity);
            return new ConditionalActivityBuilder(this, id, activity);
        }

        public ConditionalActivityBuilder AddConditionalActivity(
            string id,
            Func<TopicWorkflowContext, bool> condition,
            string trueActivityId,
            string falseActivityId) {
            var activity = ConditionalActivity.Create(id, condition, trueActivityId, falseActivityId);
            AddToFlow(activity);
            return new ConditionalActivityBuilder(this, id, activity);
        }

        // -------------------
        // Flow Management
        // -------------------

        private void AddToFlow(TopicFlowActivity activity) {
            if (_activities.ContainsKey(activity.Id))
                throw new InvalidOperationException($"Activity with ID '{activity.Id}' already exists.");

            _activities[activity.Id] = activity;
            _flow.Add(activity);

            _logger.LogInformation("Added activity {ActivityId} to flow", activity.Id);

            // If no start has been set yet, make the first added activity the default start
            if (_startActivityId == null) {
                _startActivityId = activity.Id;
            }
        }

        public FlowBuilder StartWith(string startingActivityId) {
            if (!_activities.ContainsKey(startingActivityId))
                throw new ArgumentException($"Activity '{startingActivityId}' not found.", nameof(startingActivityId));

            _startActivityId = startingActivityId;
            return this;
        }

        internal TopicFlowActivity? GetActivity(string activityId) =>
            _activities.TryGetValue(activityId, out var activity) ? activity : null;

        public TopicFlow.TopicFlow Build() {
            if (string.IsNullOrEmpty(_startActivityId))
                throw new InvalidOperationException("No starting activity has been set. Call StartWith() first.");

            // In the queue-driven model, TopicFlow already respects the order of Add()
            return _flow;
        }
    }

    public class ActivityBuilder {
        protected readonly FlowBuilder _flowBuilder;
        protected readonly string _activityId;

        public ActivityBuilder(FlowBuilder flowBuilder, string activityId) {
            _flowBuilder = flowBuilder;
            _activityId = activityId;
        }

        public string Id => _activityId;

        /// <summary>
        /// Marks this activity as the starting activity.
        /// </summary>
        public FlowBuilder AsStartingActivity() => _flowBuilder.StartWith(_activityId);
    }

    public class ConditionalActivityBuilder : ActivityBuilder {
        private readonly ConditionalActivity _conditionalActivity;

        public ConditionalActivityBuilder(FlowBuilder flowBuilder, string activityId, ConditionalActivity conditionalActivity)
            : base(flowBuilder, activityId) {
            _conditionalActivity = conditionalActivity ?? throw new ArgumentNullException(nameof(conditionalActivity));
        }

        public ConditionalActivityBuilder WithBranch(string conditionValue, string targetActivityId) {
            _conditionalActivity.AddBranch(conditionValue, targetActivityId);
            return this;
        }

        public ActivityBuilder WithBranchToSimpleActivity(string conditionValue, string activityId, Func<TopicWorkflowContext, object?> func) {
            var builder = _flowBuilder.AddSimpleActivity(activityId, func);
            _conditionalActivity.AddBranch(conditionValue, activityId);
            return builder;
        }

        public ActivityBuilder WithBranchToInteractive(string conditionValue, string activityId, string message) {
            var builder = _flowBuilder.AddInteractiveActivity(activityId, message);
            _conditionalActivity.AddBranch(conditionValue, activityId);
            return builder;
        }
    }
}
