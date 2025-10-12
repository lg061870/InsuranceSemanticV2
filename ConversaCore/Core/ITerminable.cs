using System;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.Core
{
    /// <summary>
    /// Interface for components that support explicit termination.
    /// Implementing this interface allows components to properly clean up resources,
    /// unsubscribe from events, and prepare for garbage collection.
    /// </summary>
    public interface ITerminable
    {
        /// <summary>
        /// Terminates the component, releasing resources and unsubscribing from events.
        /// </summary>
        /// <remarks>
        /// This method should be idempotent - calling it multiple times should not cause errors.
        /// After termination, the component should be considered unusable.
        /// </remarks>
        void Terminate();

        /// <summary>
        /// Asynchronously terminates the component, releasing resources and unsubscribing from events.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the termination process.</param>
        /// <returns>A task representing the asynchronous termination operation.</returns>
        /// <remarks>
        /// This method should be idempotent - calling it multiple times should not cause errors.
        /// After termination, the component should be considered unusable.
        /// </remarks>
        Task TerminateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets whether this component has been terminated.
        /// </summary>
        bool IsTerminated { get; }
    }
}