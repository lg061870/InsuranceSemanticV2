using ConversaCore.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.Topics {
    /// <summary>
    /// Represents a conversation topic that can process user messages and generate responses.
    /// </summary>
    public interface ITopic {
        string Name { get; }
        int Priority { get; }

        /// <summary>
        /// Processes a user message and returns a ChatResponse.
        /// </summary>
        Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determines if this topic can handle the given message.
        /// </summary>
        Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default);
    }
}
