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
    }
}