using System;
using System.Collections.Generic;

namespace ConversaCore.Models {
    /// <summary>
    /// Represents the result of executing a topic (one step or a whole run).
    /// </summary>
    public class TopicResult {
        public string? Response { get; set; }
        public string? AdaptiveCardJson { get; set; }
        public bool RequiresInput { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsHandled { get; set; }
        public bool KeepActive { get; set; }
        public string? NextTopicName { get; set; }
        public TopicFlow.TopicWorkflowContext? wfContext { get; set; }

        /// <summary>
        /// Optional collection of surfaced payloads from activities that requested
        /// "emit and continue" semantics.
        /// </summary>
        public List<object>? OutboxPayloads { get; set; }

        /// <summary>
        /// General-purpose event list for UI or service consumers.
        /// This is what InsuranceAgentService copies into ChatResponse.Events.
        /// </summary>
        public List<ChatEvent>? Events { get; set; }

        public bool IsAdaptiveCard => !string.IsNullOrEmpty(AdaptiveCardJson);

        // --- Factory methods ---

        public static TopicResult CreateResponse(
            string message,
            TopicFlow.TopicWorkflowContext wfContext,
            bool requiresInput = false) {
            return new TopicResult {
                Response = message,
                RequiresInput = requiresInput,
                IsHandled = true,
                wfContext = wfContext
            };
        }

        public static TopicResult CreateAdaptiveCard(
            string cardJson,
            TopicFlow.TopicWorkflowContext wfContext,
            string? message = null) {
            return new TopicResult {
                Response = message,
                AdaptiveCardJson = cardJson,
                RequiresInput = true,
                IsHandled = true,
                wfContext = wfContext
            };
        }

        public static TopicResult CreateCompleted(
            string message,
            TopicFlow.TopicWorkflowContext wfContext) {
            return new TopicResult {
                Response = message,
                IsCompleted = true,
                IsHandled = true,
                wfContext = wfContext
            };
        }

        /// <summary>
        /// Create a response while including outbox payloads that were emitted along the way.
        /// </summary>
        public static TopicResult CreateResponseWithOutbox(
            string message,
            TopicFlow.TopicWorkflowContext wfContext,
            List<object> outbox) {
            var events = new List<ChatEvent>();
            foreach (var item in outbox) {
                if (item is ChatEvent ce) {
                    events.Add(ce);
                }
                else {
                    // wrap raw object into a ChatEvent
                    events.Add(new ChatEvent {
                        Type = item.GetType().Name,
                        Payload = item
                    });
                }
            }

            return new TopicResult {
                Response = message,
                RequiresInput = false,
                IsHandled = true,
                wfContext = wfContext,
                OutboxPayloads = outbox,
                Events = events   // ✅ now properly typed
            };
        }
    }
}
