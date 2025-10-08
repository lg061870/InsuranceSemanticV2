using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.TopicFlow {
    /// <summary>
    /// An activity that contains and orchestrates a sequence of child activities.
    /// </summary>
    public class CompositeActivity : TopicFlowActivity {
        private readonly IList<TopicFlowActivity> _activities;

        /// <summary>
        /// Gets a value indicating whether the context of this composite activity is isolated.
        /// When true, changes to the context within this activity will not affect the parent context.
        /// </summary>
        public bool IsolateContext { get; set; }

        /// <summary>
        /// Gets or sets a message to display when all activities have completed.
        /// </summary>
        public string? CompleteMessage { get; set; }

        public CompositeActivity(string id, IEnumerable<TopicFlowActivity> activities)
            : base(id) {
            if (activities == null || !activities.Any())
                throw new ArgumentNullException(nameof(activities), "At least one activity must be provided.");

            _activities = new List<TopicFlowActivity>(activities);
        }

        public static CompositeActivity Create(string id, params TopicFlowActivity[] activities)
            => new CompositeActivity(id, activities);

        public static CompositeActivity Create(string id, params Action<TopicWorkflowContext>[] actions) {
            if (actions == null || actions.Length == 0)
                throw new ArgumentNullException(nameof(actions), "At least one action must be provided.");

            var activities = new List<TopicFlowActivity>();
            for (int i = 0; i < actions.Length; i++) {
                var action = actions[i] ?? throw new ArgumentNullException(nameof(actions), $"Action at index {i} is null.");
                activities.Add(SimpleActivity.Create($"{id}_Step{i + 1}", action));
            }

            return new CompositeActivity(id, activities);
        }

        /// <inheritdoc/>
        protected override async Task<ActivityResult> RunActivity(
            TopicWorkflowContext context,
            object? input = null,
            CancellationToken cancellationToken = default) {
            string currentIndexKey = $"{Id}_CurrentActivityIndex";
            int currentIndex = context.GetValue<int>(currentIndexKey, 0);

            // Setup working context (possibly isolated)
            TopicWorkflowContext workingContext = context;
            if (IsolateContext) {
                var isolatedKey = $"{Id}_IsolatedContext";
                workingContext = context.GetValue<TopicWorkflowContext>(isolatedKey)
                                 ?? new TopicWorkflowContext();

                if (!context.ContainsKey(isolatedKey))
                    context.SetValue(isolatedKey, workingContext);
            }

            // If input is passed, forward it to the current child activity
            if (input != null && currentIndex < _activities.Count) {
                var child = _activities[currentIndex];
                var result = await child.RunAsync(workingContext, input, cancellationToken);

                if (result.IsWaiting)
                    return result;

                currentIndex++;
                context.SetValue(currentIndexKey, currentIndex);
            }

            // Execute sequentially until done or waiting
            while (currentIndex < _activities.Count) {
                var child = _activities[currentIndex];
                var result = await child.RunAsync(workingContext, null, cancellationToken);

                if (result.IsWaiting)
                    return result;

                currentIndex++;
                context.SetValue(currentIndexKey, currentIndex);
            }

            // Reset index for future runs
            context.SetValue(currentIndexKey, 0);

            // Copy isolated outputs back to parent context
            if (IsolateContext)
                context.SetValue(Id, workingContext);

            // Completion message (optional)
            if (!string.IsNullOrEmpty(CompleteMessage))
                return ActivityResult.Continue(CompleteMessage);

            // Otherwise, just advance to the next activity in the queue
            return ActivityResult.Continue();

        }
    }
}
