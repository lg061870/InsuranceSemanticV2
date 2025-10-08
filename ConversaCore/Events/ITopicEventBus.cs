using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConversaCore.Events
{
    /// <summary>
    /// Interface for the topic event bus
    /// </summary>
    public interface ITopicEventBus
    {
        /// <summary>
        /// Subscribes to a specific type of topic event.
        /// </summary>
        /// <param name="eventType">The type of event to subscribe to.</param>
        /// <param name="handler">The handler to call when the event occurs.</param>
        void Subscribe(TopicEventType eventType, Func<TopicEvent, Task> handler);

        /// <summary>
        /// Unsubscribes from a specific type of topic event.
        /// </summary>
        /// <param name="eventType">The type of event to unsubscribe from.</param>
        /// <param name="handler">The handler to remove.</param>
        void Unsubscribe(TopicEventType eventType, Func<TopicEvent, Task> handler);

        /// <summary>
        /// Publishes a topic event.
        /// </summary>
        /// <param name="topicEvent">The topic event to publish.</param>
        Task PublishAsync(TopicEvent topicEvent);

        /// <summary>
        /// Publishes a topic event of the specified type.
        /// </summary>
        /// <param name="eventType">The type of event to publish.</param>
        /// <param name="topicName">The name of the topic associated with the event.</param>
        /// <param name="conversationId">The ID of the conversation associated with the event.</param>
        /// <param name="data">Any additional data associated with the event.</param>
        /// <param name="correlationId">An optional correlation ID for related events.</param>
        Task PublishAsync(
            TopicEventType eventType, 
            string topicName, 
            string conversationId, 
            object data = null,
            string correlationId = null);
        
        /// <summary>
        /// Gets the event history for a specific conversation.
        /// </summary>
        /// <param name="conversationId">The conversation ID.</param>
        /// <returns>A list of events for the specified conversation.</returns>
        IReadOnlyList<TopicEvent> GetEventHistory(string conversationId);

        /// <summary>
        /// Gets the event history for a specific topic and conversation.
        /// </summary>
        /// <param name="topicName">The topic name.</param>
        /// <param name="conversationId">The conversation ID.</param>
        /// <returns>A list of events for the specified topic and conversation.</returns>
        IReadOnlyList<TopicEvent> GetEventHistory(string topicName, string conversationId);
        
        /// <summary>
        /// Gets events that are part of the same correlation chain.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <returns>A list of correlated events.</returns>
        IReadOnlyList<TopicEvent> GetCorrelatedEvents(string correlationId);
    }
}