using System.Collections.Generic;

namespace ConversaCore.TopicTool.Models
{
    /// <summary>
    /// Schema for a ConversaCore topic specification used by the Topic Tool.
    /// This is intentionally minimal for now and can evolve with the SDK.
    /// </summary>
    public class TopicSpec
    {
        public string Name { get; set; } = string.Empty;

        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// High-level description of the topic's purpose.
        /// </summary>
        public string IntentDescription { get; set; } = string.Empty;

        /// <summary>
        /// Keywords that should route to this topic.
        /// </summary>
        public List<string> IntentKeywords { get; set; } = new List<string>();

        /// <summary>
        /// Optional routing priority (higher means more preferred when conflicts occur).
        /// </summary>
        public double? Priority { get; set; }

        /// <summary>
        /// Narrative description of the expected conversation flow.
        /// </summary>
        public string FlowNarrative { get; set; } = string.Empty;

        /// <summary>
        /// Whether the topic uses adaptive cards.
        /// </summary>
        public bool UsesAdaptiveCards { get; set; }
        /// <summary>
        /// Optional subtopic names that this topic may trigger.
        /// </summary>
        public List<string> Subtopics { get; set; } = new List<string>();

        /// <summary>
        /// Arbitrary flags/extensions for future features.
        /// </summary>
        public Dictionary<string, string> Flags { get; set; } = new Dictionary<string, string>();
    }
}
