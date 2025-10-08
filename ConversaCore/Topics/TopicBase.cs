using ConversaCore.Models;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.Topics {
    /// <summary>
    /// Base class for all conversation topics built on TopicFlow.
    /// </summary>
    public abstract class TopicBase : ITopic {
        /// <summary>
        /// Gets or sets the name of this topic.
        /// </summary>
        public virtual string Name { get; protected set; }

        /// <summary>
        /// Gets or sets the priority of this topic when evaluating multiple topics.
        /// Higher priority topics are checked first.
        /// </summary>
        public virtual int Priority { get; protected set; } = 0;

        /// <summary>
        /// The workflow context for this topic.
        /// </summary>
        protected TopicWorkflowContext Context { get; }

        /// <summary>
        /// The workflow flow for this topic.
        /// </summary>
        protected TopicFlow.TopicFlow? Flow { get; set; }

        /// <summary>
        /// Initializes a new instance of the TopicBase class.
        /// </summary>
        protected TopicBase(TopicWorkflowContext context) {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Name = GetType().Name;
            // DEBUG: Tracking Context Lifecycle
            Context.SetValue($"{Name}_created", DateTime.UtcNow.ToString("o"));
        }

        /// <summary>
        /// Determines whether this topic can handle the given user message.
        /// Default implementation returns 0 confidence.
        /// </summary>
        public virtual Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
            return Task.FromResult(0.0f);
        }

        /// <summary>
        /// Processes a user message within this topic.
        /// Derived classes should construct a TopicFlow and run it here.
        /// </summary>
        public abstract Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs the topic flow from the beginning.
        /// </summary>
        protected async Task<TopicResult> RunFlowAsync(CancellationToken cancellationToken = default) {
            if (Flow == null)
                throw new InvalidOperationException("Flow has not been built for this topic.");
            // DEBUG: Tracking Context Lifecycle
            Context.SetValue($"{Name}_run", DateTime.UtcNow.ToString("o"));
            return await Flow.RunAsync(cancellationToken);
        }

        /// <summary>
        /// Resumes the topic flow with user input.
        /// </summary>
        protected async Task<TopicResult> ResumeFlowAsync(string message, CancellationToken cancellationToken = default) {
            if (Flow == null)
                throw new InvalidOperationException("Flow has not been built for this topic.");
            return await Flow.ResumeAsync(message, cancellationToken);
        }
    }
}
