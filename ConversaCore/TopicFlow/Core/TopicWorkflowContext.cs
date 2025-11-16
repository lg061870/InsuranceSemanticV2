using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ConversaCore.TopicFlow
{
    /// <summary>
    /// A key-value store for workflow state.
    /// </summary>
    public class TopicWorkflowContext
    {
    // (keep only one declaration below)

        // DEBUG: Tracking Context Lifecycle
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kvp in _values)
            {
                sb.Append(kvp.Key);
                sb.Append(": ");
                if (kvp.Value is string s)
                    sb.Append(s);
                else if (kvp.Value != null)
                    sb.Append(kvp.Value.GetType().ToString());
                else
                    sb.Append("null");
                sb.AppendLine();
            }
            return sb.ToString();
        }
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        
        /// <summary>
        /// Sets a value in the workflow context.
        /// </summary>
        /// <param name="key">The key to store the value under.</param>
        /// <param name="value">The value to store.</param>
        public void SetValue(string key, object? value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));
                
            if (value == null)
            {
                if (_values.ContainsKey(key))
                    _values.Remove(key);
                return;
            }
            
            _values[key] = value;
        }

        /// <summary>
        /// Gets a value from the workflow context.
        /// </summary>
        /// <typeparam name="T">The type to convert the value to.</typeparam>
        /// <param name="key">The key to retrieve the value for.</param>
        /// <returns>The value if found and convertible to T; default(T) otherwise.</returns>
        public T? GetValue<T>(string key) {
            if (string.IsNullOrEmpty(key) || !_values.ContainsKey(key))
                return default;

            var value = _values[key];

            if (value is T typedValue)
                return typedValue;

            try {
                // 🔹 Handle JsonElement (common in JSON-based contexts)
                if (value is JsonElement jsonElement) {
                    object? unwrapped = jsonElement.ValueKind switch {
                        JsonValueKind.String => jsonElement.GetString(),
                        JsonValueKind.Number => jsonElement.TryGetInt64(out var i64) ? i64 : jsonElement.TryGetDouble(out var dbl) ? dbl : (object?)null,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null or JsonValueKind.Undefined => null,
                        _ => jsonElement.ToString()
                    };

                    // Convert again if needed
                    if (unwrapped is T direct)
                        return direct;

                    if (unwrapped != null)
                        return (T)Convert.ChangeType(unwrapped, typeof(T));
                }

                // Fallback: normal conversion
                return (T)Convert.ChangeType(value, typeof(T));
            } catch {
                return default;
            }
        }


        /// <summary>
        /// Gets a value from the workflow context or a default value if not found.
        /// </summary>
        /// <typeparam name="T">The type to convert the value to.</typeparam>
        /// <param name="key">The key to retrieve the value for.</param>
        /// <param name="defaultValue">The default value to return if the key is not found.</param>
        /// <returns>The value if found and convertible to T; defaultValue otherwise.</returns>
        public T GetValue<T>(string key, T defaultValue)
        {
            var value = GetValue<T>(key);
            return value != null ? value : defaultValue;
        }
        
        /// <summary>
        /// Checks if the workflow context contains a specific key.
        /// </summary>
        /// <param name="key">The key to check for.</param>
        /// <returns>True if the key exists; false otherwise.</returns>
        public bool ContainsKey(string key)
        {
            return !string.IsNullOrEmpty(key) && _values.ContainsKey(key);
        }
        
        /// <summary>
        /// Gets all keys in the workflow context.
        /// </summary>
        /// <returns>An enumerable of all keys.</returns>
        public IEnumerable<string> GetKeys()
        {
            return _values.Keys;
        }
        
        /// <summary>
        /// Clears all values from the workflow context.
        /// </summary>
        public void Clear()
        {
            _values.Clear();
        }

        public void RemoveValue(string key) {
            if (string.IsNullOrEmpty(key)) return;
            _values.Remove(key);
        }


        /// <summary>
        /// Gets the number of items in the workflow context.
        /// </summary>
        public int Count => _values.Count;
    }
}