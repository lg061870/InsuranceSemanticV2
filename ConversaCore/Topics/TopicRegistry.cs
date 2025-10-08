using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.Topics {
    /// <summary>
    /// Simple registry for all available topics in the system.
    /// </summary>
    public class TopicRegistry {
        private readonly List<ITopic> _topics = new();

        /// <summary>
        /// Registers a topic with the registry.
        /// </summary>
        public void RegisterTopic(ITopic topic) {
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            // Avoid duplicate registrations
            if (!_topics.Any(t => t.Name.Equals(topic.Name, StringComparison.OrdinalIgnoreCase))) {
                _topics.Add(topic);
            }
        }

        /// <summary>
        /// Gets all registered topics.
        /// </summary>
        public IReadOnlyList<ITopic> GetAllTopics() => _topics.AsReadOnly();

        /// <summary>
        /// Gets a topic by name, or null if not found.
        /// </summary>
        public ITopic? GetTopic(string name) {
            return _topics.FirstOrDefault(t =>
                t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Clears all topics from the registry.
        /// </summary>
        internal void Clear() => _topics.Clear();

        /// <summary>
        /// Finds the best topic to handle a given message.
        /// </summary>
        public async Task<(ITopic? Topic, float Confidence)> FindBestTopicAsync(
            string message,
            Context.IConversationContext context,
            CancellationToken cancellationToken = default) {
            if (!_topics.Any())
                return (null, 0f);

            ITopic? bestTopic = null;
            float bestConfidence = 0f;

            foreach (var topic in _topics.OrderByDescending(t => t.Priority)) {
                try {
                    var confidence = await topic.CanHandleAsync(message, cancellationToken);
                    if (confidence > bestConfidence) {
                        bestConfidence = confidence;
                        bestTopic = topic;
                    }
                } catch {
                    // Skip any failing topics
                }
            }

            // fallback to Default topic if no confident match
            if (bestTopic == null || bestConfidence < 0.3f) {
                bestTopic = GetTopic("Default");
                bestConfidence = 1.0f;
            }

            return (bestTopic, bestConfidence);
        }
    }
}
