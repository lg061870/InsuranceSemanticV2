using System.Collections.Generic;
using System.Threading.Tasks;
using ConversaCore.Cards;
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

        // === STRUCTURED MODEL STORAGE ===
        
        /// <summary>
        /// Sets a strongly-typed model in the conversation context with a specific key.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <param name="key">The key to store the model under.</param>
        /// <param name="model">The model to store.</param>
        void SetModel<T>(string key, T model) where T : BaseCardModel;
        
        /// <summary>
        /// Sets a strongly-typed model in the conversation context using the type name as the key.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <param name="model">The model to store.</param>
        void SetModel<T>(T model) where T : BaseCardModel;
        
        /// <summary>
        /// Gets a strongly-typed model from the conversation context by key.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <param name="key">The key to retrieve the model for.</param>
        /// <returns>The model if found; otherwise, null.</returns>
        T? GetModel<T>(string key) where T : BaseCardModel;
        
        /// <summary>
        /// Gets a strongly-typed model from the conversation context using the type name as the key.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <returns>The model if found; otherwise, null.</returns>
        T? GetModel<T>() where T : BaseCardModel;
        
        /// <summary>
        /// Checks if a model of the specified type exists in the context.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <param name="key">The key to check for. If null, uses the type name.</param>
        /// <returns>True if the model exists; otherwise, false.</returns>
        bool HasModel<T>(string? key = null) where T : BaseCardModel;
        
        /// <summary>
        /// Gets all models of a specific type from the conversation context.
        /// Useful for models that can have multiple instances (e.g., beneficiaries, dependents).
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <returns>A list of all models of the specified type.</returns>
        List<T> GetModels<T>() where T : BaseCardModel;
        
        /// <summary>
        /// Removes a model from the conversation context.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <param name="key">The key to remove. If null, uses the type name.</param>
        /// <returns>True if the model was removed; otherwise, false.</returns>
        bool RemoveModel<T>(string? key = null) where T : BaseCardModel;

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

        // === TOPIC EXECUTION STACK (for sub-topic calls) ===
        
        /// <summary>
        /// Pushes a topic onto the execution stack when calling a sub-topic.
        /// </summary>
        /// <param name="callingTopicName">The name of the topic making the call.</param>
        /// <param name="subTopicName">The name of the sub-topic being called.</param>
        /// <param name="resumeData">Optional data to help resume the calling topic.</param>
        void PushTopicCall(string callingTopicName, string subTopicName, object? resumeData = null);
        
        /// <summary>
        /// Pops the most recent topic call from the execution stack when a sub-topic completes.
        /// </summary>
        /// <param name="completionData">Optional data returned by the completed sub-topic.</param>
        /// <returns>Information about the topic to resume, or null if stack is empty.</returns>
        TopicCallInfo? PopTopicCall(object? completionData = null);
        
        /// <summary>
        /// Gets the current topic execution stack depth.
        /// </summary>
        /// <returns>The number of nested topic calls currently active.</returns>
        int GetTopicCallDepth();
        
        /// <summary>
        /// Checks if a topic is currently in the execution stack to prevent cycles.
        /// </summary>
        /// <param name="topicName">The topic name to check.</param>
        /// <returns>True if the topic is already in the call stack.</returns>
        bool IsTopicInCallStack(string topicName);

        /// <summary>
        /// Gets the currently executing topic name (top of stack).
        /// </summary>
        /// <returns>The name of the currently executing topic, or null if none.</returns>
        string? GetCurrentExecutingTopic();

        /// <summary>
        /// Signals that a topic has completed and should be removed from execution tracking.
        /// </summary>
        /// <param name="topicName">The name of the completed topic.</param>
        /// <param name="completionData">Optional data about the completion.</param>
        void SignalTopicCompletion(string topicName, object? completionData = null);

        /// <summary>
        /// Resets the conversation context to its initial state.
        /// Clears all data, topic history, and execution stacks.
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Information about a topic call for managing the execution stack.
    /// </summary>
    public class TopicCallInfo
    {
        /// <summary>
        /// The name of the topic that made the call.
        /// </summary>
        public string CallingTopicName { get; set; } = string.Empty;
        
        /// <summary>
        /// The name of the sub-topic that was called.
        /// </summary>
        public string SubTopicName { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional data to help resume the calling topic.
        /// </summary>
        public object? ResumeData { get; set; }
        
        /// <summary>
        /// Optional data returned by the completed sub-topic.
        /// </summary>
        public object? CompletionData { get; set; }
        
        /// <summary>
        /// When this topic call was initiated.
        /// </summary>
        public DateTime CallTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the sub-topic completed (if applicable).
        /// </summary>
        public DateTime? CompletionTime { get; set; }
    }
}