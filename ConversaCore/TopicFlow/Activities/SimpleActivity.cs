using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.TopicFlow {
    /// <summary>
    /// An activity that executes a simple action or emits a static message,
    /// then immediately continues to the next activity.
    /// </summary>
    public class SimpleActivity : TopicFlowActivity {
        private readonly Func<TopicWorkflowContext, object?, Task<object?>>? _action;
        private readonly string? _message;

        /// <summary>
        /// Initializes a new instance of <see cref="SimpleActivity"/> with an async action.
        /// </summary>
        public SimpleActivity(string id, Func<TopicWorkflowContext, object?, Task<object?>> action)
            : base(id) {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        /// <summary>
        /// Initializes a new instance of <see cref="SimpleActivity"/> with a static message.
        /// </summary>
        public SimpleActivity(string id, string message)
            : base(id) {
            _message = message ?? throw new ArgumentNullException(nameof(message));
        }

        // ------------------------------
        // Factory Helpers
        // ------------------------------

        public static SimpleActivity Create(string id, Action<TopicWorkflowContext> action) {
            if (action == null) throw new ArgumentNullException(nameof(action));

            return new SimpleActivity(id, (ctx, _) => {
                action(ctx);
                return Task.FromResult<object?>(null);
            });
        }

        public static SimpleActivity Create(string id, Func<TopicWorkflowContext, object?> func) {
            if (func == null) throw new ArgumentNullException(nameof(func));

            return new SimpleActivity(id, (ctx, _) => Task.FromResult(func(ctx)));
        }

        public static SimpleActivity Create(string id, Func<TopicWorkflowContext, Task> action) {
            if (action == null) throw new ArgumentNullException(nameof(action));

            return new SimpleActivity(id, async (ctx, _) => {
                await action(ctx);
                return null;
            });
        }

        // ------------------------------
        // Execution
        // ------------------------------

        /// <inheritdoc/>
        protected override async Task<ActivityResult> RunActivity(
            TopicWorkflowContext context,
            object? input = null,
            CancellationToken cancellationToken = default) {
            TransitionTo(ActivityState.Running, input);

            object? modelContext = null;

            try {
                if (_action != null) {
                    modelContext = await _action(context, input);
                }

                if (!string.IsNullOrEmpty(_message)) {
                    TransitionTo(ActivityState.Completed, _message);
                    return ActivityResult.Continue(_message, modelContext);
                }

                TransitionTo(ActivityState.Completed, modelContext);
                return ActivityResult.Continue(modelContext ?? new object());
            } catch (Exception ex) {
                TransitionTo(ActivityState.Failed, ex);
                throw;
            }
        }
    }
}
