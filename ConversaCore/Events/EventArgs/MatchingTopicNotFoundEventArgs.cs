using System;

namespace ConversaCore.Events; 
/// <summary>
/// Event args raised when no matching topic was found for a user message.
/// </summary>
public class MatchingTopicNotFoundEventArgs : EventArgs {
    /// <summary>
    /// Gets the user message that failed to match a topic.
    /// </summary>
    public string UserMessage { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MatchingTopicNotFoundEventArgs"/>.
    /// </summary>
    /// <param name="userMessage">The message that could not be matched.</param>
    public MatchingTopicNotFoundEventArgs(string userMessage) {
        UserMessage = userMessage ?? throw new ArgumentNullException(nameof(userMessage));
    }
}
