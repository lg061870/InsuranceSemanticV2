using ConversaCore.TopicFlow;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConversaCore.Cards {

    /// <summary>
    /// Base class for all adaptive card models, providing common functionality
    /// including support for embedded continuation prompts in repeat scenarios.
    /// </summary>
    public abstract class BaseCardModel {
        /// <summary>
        /// User response for continuation decisions when used in repeat scenarios.
        /// This field captures choices like "continue", "stop", "yes", "no" etc.
        /// </summary>
        [JsonPropertyName("user_response")]
        public string? UserResponse { get; set; }

        // Common metadata (if needed)
        [JsonIgnore]
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Automatically updates the provided workflow context with all model fields.
        /// Uses JSON serialization for deterministic property mapping.
        /// </summary>
        public virtual void UpdateContext(TopicWorkflowContext context) {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            Console.WriteLine($"[BaseCardModel] 🔄 UpdateContext called for {GetType().Name}");

            // Serialize this model instance to JSON
            var json = JsonSerializer.Serialize(this, this.GetType(), new JsonSerializerOptions {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            Console.WriteLine($"[BaseCardModel] 📄 Serialized JSON: {json}");

            // Deserialize into a loose dictionary
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;

            Console.WriteLine($"[BaseCardModel] 📦 Dictionary has {dict.Count} entries");

            // Normalize all values (unwrap JsonElement, etc.)
            foreach (var kvp in dict) {
                object? value = kvp.Value;

                // 🔹 If value is a JsonElement, unwrap it to a native .NET type
                if (value is JsonElement jsonElement) {
                    value = jsonElement.ValueKind switch {
                        JsonValueKind.String => jsonElement.GetString(),
                        JsonValueKind.Number =>
                            jsonElement.TryGetInt64(out var i64) ? i64 :
                            jsonElement.TryGetDouble(out var dbl) ? dbl : (object?)null,
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null or JsonValueKind.Undefined => null,
                        _ => jsonElement.ToString()
                    };
                }

                // Push clean value into workflow context
                context.SetValue(kvp.Key, value);
                Console.WriteLine($"[BaseCardModel] ✅ Stored: '{kvp.Key}' = '{value}' (Type: {value?.GetType().Name ?? "null"})");
            }

            // Optional: record class name + timestamp for tracing
            context.SetValue($"{GetType().Name}_updated", DateTime.UtcNow);
            Console.WriteLine($"[BaseCardModel] ✅ UpdateContext completed for {GetType().Name}");
        }
    }
}


