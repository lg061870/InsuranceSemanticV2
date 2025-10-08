using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using ConversaCore.Cards;
#nullable enable

namespace ConversaCore.Context
{
    /// <summary>
    /// Default implementation of IConversationContext.
    /// </summary>
    public class ConversationContext : IConversationContext
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();
        private readonly List<string> _topicHistory = new List<string>();
        private readonly Stack<TopicCallInfo> _topicCallStack = new Stack<TopicCallInfo>();

        /// <summary>
        /// Gets the conversation ID.
        /// </summary>
        public string ConversationId { get; }

        /// <summary>
        /// Gets the user ID.
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// Gets the current active topic name.
        /// </summary>
        public string? CurrentTopicName { get; private set; }

        /// <summary>
        /// Gets the history of topics that have been active in this conversation.
        /// </summary>
        public IReadOnlyList<string> TopicHistory => _topicHistory.AsReadOnly();

        /// <summary>
        /// Gets or sets the queue of topics to process in sequence.
        /// </summary>
        public Queue<string> TopicChain { get; } = new Queue<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationContext"/> class.
        /// </summary>
        /// <param name="conversationId">The conversation ID.</param>
        /// <param name="userId">The user ID.</param>
        public ConversationContext(string conversationId, string userId)
        {
            ConversationId = conversationId;
            UserId = userId;
        }

        /// <summary>
        /// Sets a value in the conversation context.
        /// </summary>
        /// <param name="key">The key for the value.</param>
        /// <param name="value">The value to set.</param>
        public void SetValue(string key, object value)
        {
            _data[key] = value;
        }

        /// <summary>
        /// Gets a value from the conversation context.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The key for the value.</param>
        /// <param name="defaultValue">The default value to return if the key is not found.</param>
        /// <returns>The value if found; otherwise, the default value.</returns>
        public T GetValue<T>(string key, T defaultValue = default!)
        {
            if (_data.TryGetValue(key, out var value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
                
                try
                {
                    // Try to convert using JSON serialization
                    string json = JsonSerializer.Serialize(value);
                    var result = JsonSerializer.Deserialize<T>(json);
                    return result ?? defaultValue;
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
        }
        
        /// <summary>
        /// Tries to get a value from the conversation context.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The key for the value.</param>
        /// <param name="value">The value if found.</param>
        /// <returns>True if the value was found; otherwise, false.</returns>
        public bool TryGetValue<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var objValue))
            {
                if (objValue is T typedValue)
                {
                    value = typedValue;
                    return true;
                }
                
                try
                {
                    // Try to convert using JSON serialization
                    string json = JsonSerializer.Serialize(objValue);
                    var deserializedValue = JsonSerializer.Deserialize<T>(json);
                    if (deserializedValue != null)
                    {
                        value = deserializedValue;
                        return true;
                    }
                }
                catch
                {
                    // Conversion failed
                }
            }

            value = default!;
            return false;
        }

        /// <summary>
        /// Checks if a key exists in the context.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        public bool HasValue(string key)
        {
            return _data.ContainsKey(key);
        }

        /// <summary>
        /// Gets all conversation data.
        /// </summary>
        /// <returns>A dictionary of all conversation data.</returns>
        public Dictionary<string, object> GetAllData()
        {
            return new Dictionary<string, object>(_data);
        }

        /// <summary>
        /// Sets the current active topic name.
        /// </summary>
        /// <param name="topicName">The topic name to set as active.</param>
        public void SetCurrentTopic(string topicName)
        {
            CurrentTopicName = topicName;
            AddTopicToHistory(topicName);
        }

        /// <summary>
        /// Adds a topic to the topic history.
        /// </summary>
        /// <param name="topicName">The topic name to add to history.</param>
        public void AddTopicToHistory(string topicName)
        {
            if (!string.IsNullOrEmpty(topicName))
            {
                _topicHistory.Add(topicName);
            }
        }

        /// <summary>
        /// Adds a topic to the topic chain.
        /// </summary>
        /// <param name="topicName">The topic name to add to the chain.</param>
        public void AddTopicToChain(string topicName)
        {
            if (!string.IsNullOrEmpty(topicName))
            {
                TopicChain.Enqueue(topicName);
            }
        }

        // === STRUCTURED MODEL STORAGE IMPLEMENTATION ===
        
        /// <summary>
        /// Sets a strongly-typed model in the conversation context with a specific key.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <param name="key">The key to store the model under.</param>
        /// <param name="model">The model to store.</param>
        public void SetModel<T>(string key, T model) where T : BaseCardModel
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
            if (model == null)
                throw new ArgumentNullException(nameof(model));
                
            SetValue($"Model_{key}", model);
        }
        
        /// <summary>
        /// Sets a strongly-typed model in the conversation context using the type name as the key.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <param name="model">The model to store.</param>
        public void SetModel<T>(T model) where T : BaseCardModel
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
                
            var key = typeof(T).Name;
            SetModel(key, model);
        }
        
        /// <summary>
        /// Gets a strongly-typed model from the conversation context by key.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <param name="key">The key to retrieve the model for.</param>
        /// <returns>The model if found; otherwise, null.</returns>
        public T? GetModel<T>(string key) where T : BaseCardModel
        {
            if (string.IsNullOrEmpty(key))
                return null;
                
            return GetValue<T>($"Model_{key}");
        }
        
        /// <summary>
        /// Gets a strongly-typed model from the conversation context using the type name as the key.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <returns>The model if found; otherwise, null.</returns>
        public T? GetModel<T>() where T : BaseCardModel
        {
            var key = typeof(T).Name;
            return GetModel<T>(key);
        }
        
        /// <summary>
        /// Checks if a model of the specified type exists in the context.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <param name="key">The key to check for. If null, uses the type name.</param>
        /// <returns>True if the model exists; otherwise, false.</returns>
        public bool HasModel<T>(string? key = null) where T : BaseCardModel
        {
            var modelKey = key ?? typeof(T).Name;
            return HasValue($"Model_{modelKey}");
        }
        
        /// <summary>
        /// Gets all models of a specific type from the conversation context.
        /// Useful for models that can have multiple instances (e.g., beneficiaries, dependents).
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <returns>A list of all models of the specified type.</returns>
        public List<T> GetModels<T>() where T : BaseCardModel
        {
            var typeName = typeof(T).Name;
            var models = new List<T>();
            
            foreach (var kvp in _data)
            {
                if (kvp.Key.StartsWith($"Model_{typeName}") && kvp.Value is T model)
                {
                    models.Add(model);
                }
            }
            
            return models;
        }
        
        /// <summary>
        /// Removes a model from the conversation context.
        /// </summary>
        /// <typeparam name="T">The type of the model (must inherit from BaseCardModel).</typeparam>
        /// <param name="key">The key to remove. If null, uses the type name.</param>
        /// <returns>True if the model was removed; otherwise, false.</returns>
        public bool RemoveModel<T>(string? key = null) where T : BaseCardModel
        {
            var modelKey = key ?? typeof(T).Name;
            var fullKey = $"Model_{modelKey}";
            
            if (_data.ContainsKey(fullKey))
            {
                _data.Remove(fullKey);
                return true;
            }
            
            return false;
        }

        // === TOPIC EXECUTION STACK IMPLEMENTATION ===

        /// <summary>
        /// Pushes a topic onto the execution stack when calling a sub-topic.
        /// </summary>
        /// <param name="callingTopicName">The name of the topic making the call.</param>
        /// <param name="subTopicName">The name of the sub-topic being called.</param>
        /// <param name="resumeData">Optional data to help resume the calling topic.</param>
        public void PushTopicCall(string callingTopicName, string subTopicName, object? resumeData = null)
        {
            var callInfo = new TopicCallInfo
            {
                CallingTopicName = callingTopicName,
                SubTopicName = subTopicName,
                ResumeData = resumeData,
                CallTime = DateTime.UtcNow
            };
            
            _topicCallStack.Push(callInfo);
        }

        /// <summary>
        /// Pops the most recent topic call from the execution stack when a sub-topic completes.
        /// </summary>
        /// <param name="completionData">Optional data returned by the completed sub-topic.</param>
        /// <returns>Information about the topic to resume, or null if stack is empty.</returns>
        public TopicCallInfo? PopTopicCall(object? completionData = null)
        {
            if (_topicCallStack.Count == 0)
                return null;

            var callInfo = _topicCallStack.Pop();
            callInfo.CompletionData = completionData;
            callInfo.CompletionTime = DateTime.UtcNow;
            
            return callInfo;
        }

        /// <summary>
        /// Gets the current topic execution stack depth.
        /// </summary>
        /// <returns>The number of nested topic calls currently active.</returns>
        public int GetTopicCallDepth()
        {
            return _topicCallStack.Count;
        }

        /// <summary>
        /// Checks if a topic is currently in the execution stack to prevent cycles.
        /// </summary>
        /// <param name="topicName">The topic name to check.</param>
        /// <returns>True if the topic is already in the call stack.</returns>
        public bool IsTopicInCallStack(string topicName)
        {
            return _topicCallStack.Any(call => 
                call.CallingTopicName.Equals(topicName, StringComparison.OrdinalIgnoreCase) ||
                call.SubTopicName.Equals(topicName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the currently executing topic name (top of stack).
        /// </summary>
        /// <returns>The name of the currently executing topic, or null if none.</returns>
        public string? GetCurrentExecutingTopic()
        {
            if (_topicCallStack.Count == 0)
                return CurrentTopicName;
                
            return _topicCallStack.Peek().SubTopicName;
        }

        /// <summary>
        /// Signals that a topic has completed and should be removed from execution tracking.
        /// </summary>
        /// <param name="topicName">The name of the completed topic.</param>
        /// <param name="completionData">Optional data about the completion.</param>
        public void SignalTopicCompletion(string topicName, object? completionData = null)
        {
            // For now, this is primarily used for logging/debugging
            // The actual stack management happens in PopTopicCall
            // In the future, this could trigger events or additional cleanup
        }
    }
}