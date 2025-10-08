using System.Collections.Generic;
using System.Threading.Tasks;
#nullable enable

namespace ConversaCore.Context
{
    /// <summary>
    /// Represents the context for a conversation.
    /// </summary>
    public interface IConversationContext
    {
        /// <summary>
        /// Gets the conversation ID.
        /// </summary>
        string ConversationId { get; }

        /// <summary>
        /// Gets the user ID.
        /// </summary>
        string UserId { get; }

        /// <summary>
        /// Gets the current active topic name.
        /// </summary>
        string? CurrentTopicName { get; }
        
        /// <summary>
        /// Gets the history of topics that have been active in this conversation.
        /// </summary>
        IReadOnlyList<string> TopicHistory { get; }
        
        /// <summary>
        /// Gets or sets the queue of topics to process in sequence.
        /// </summary>
        Queue<string> TopicChain { get; }

        /// <summary>
        /// Sets a value in the conversation context.
        /// </summary>
        /// <param name="key">The key for the value.</param>
        /// <param name="value">The value to set.</param>
        void SetValue(string key, object value);

        /// <summary>
        /// Gets a value from the conversation context.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The key for the value.</param>
        /// <param name="defaultValue">The default value to return if the key is not found.</param>
        /// <returns>The value if found; otherwise, the default value.</returns>
        T GetValue<T>(string key, T defaultValue = default);
        
        /// <summary>
        /// Tries to get a value from the conversation context.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The key for the value.</param>
        /// <param name="value">The value if found.</param>
        /// <returns>True if the value was found; otherwise, false.</returns>
        bool TryGetValue<T>(string key, out T value);

        /// <summary>
        /// Checks if a key exists in the context.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        bool HasValue(string key);

        /// <summary>
        /// Gets all conversation data.
        /// </summary>
        /// <returns>A dictionary of all conversation data.</returns>
        Dictionary<string, object> GetAllData();

        /// <summary>
        /// Sets the current active topic name.
        /// </summary>
        /// <param name="topicName">The topic name to set as active.</param>
        void SetCurrentTopic(string topicName);
        
        /// <summary>
        /// Adds a topic to the topic history.
        /// </summary>
        /// <param name="topicName">The topic name to add to history.</param>
        void AddTopicToHistory(string topicName);
        
        /// <summary>
        /// Adds a topic to the topic chain.
        /// </summary>
        /// <param name="topicName">The topic name to add to the chain.</param>
        void AddTopicToChain(string topicName);
    }
}