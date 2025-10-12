using System;

namespace ConversaCore.Events {
    /// <summary>
    /// Defines the types of topic events.
    /// </summary>
    public enum TopicEventType {
        /// <summary>A topic has been activated.</summary>
        TopicActivated,

        /// <summary>A topic has been deactivated.</summary>
        TopicDeactivated,

        /// <summary>A topic has completed its flow.</summary>
        TopicCompleted,

        /// <summary>A topic is requesting a transition to another topic.</summary>
        TopicTransitionRequested,

        /// <summary>A new user message has been received.</summary>
        UserMessageReceived,

        /// <summary>A bot message has been sent.</summary>
        BotMessageSent,

        /// <summary>An adaptive card has been displayed.</summary>
        AdaptiveCardDisplayed,

        /// <summary>An adaptive card action has been submitted.</summary>
        AdaptiveCardSubmitted,
        
        /// <summary>The conversation has been reset.</summary>
        ConversationReset,
        
        /// <summary>The conversation has been completed.</summary>
        ConversationCompleted,
        
        /// <summary>An error occurred in the conversation.</summary>
        ConversationError
    }

    /// <summary>
    /// Represents a topic-related event.
    /// Immutable record style for safer event handling.
    /// </summary>
    public record TopicEvent(
        TopicEventType EventType,
        string TopicName,
        string ConversationId,
        object? Data = null,
        string? CorrelationId = null
    ) {
        /// <summary>Unique ID for the event.</summary>
        public string EventId { get; init; } = Guid.NewGuid().ToString();

        /// <summary>Timestamp of the event (UTC).</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
