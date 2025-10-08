using System.Text.Json.Serialization;

namespace ConversaCore.Cards
{
    /// <summary>
    /// Base class for all adaptive card models, providing common functionality
    /// including support for embedded continuation prompts in repeat scenarios.
    /// </summary>
    public abstract class BaseCardModel
    {
        /// <summary>
        /// User response for continuation decisions when used in repeat scenarios.
        /// This field captures choices like "continue", "stop", "yes", "no" etc.
        /// </summary>
        [JsonPropertyName("user_response")]
        public string? UserResponse { get; set; }
    }
}