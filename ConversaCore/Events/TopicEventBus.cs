using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.Extensions.Logging;
using ConversaCore.Core;

namespace ConversaCore.Events {
    /// <summary>
    /// Provides an event bus for topic-related events.
    /// Thread-safe, immutable history, singleton pattern.
    /// Implements ITerminable for proper resource cleanup.
    /// </summary>
    public sealed class TopicEventBus : ITopicEventBus {
        private bool _isTerminated = false;
        private readonly ILogger<TopicEventBus>? _logger = null;
        private static readonly Lazy<TopicEventBus> _instance =
            new(() => new TopicEventBus());

        /// <summary>
        /// Gets the singleton instance of the topic event bus.
        /// </summary>
        public static TopicEventBus Instance => _instance.Value;

        // Subscribers keyed by event type
        private readonly ConcurrentDictionary<TopicEventType, List<Func<TopicEvent, Task>>> _subscribers = new();

        // Event history
        private readonly ConcurrentQueue<TopicEvent> _eventHistory = new();

        private const int MaxHistorySize = 1000;

        public bool IsTerminated => _isTerminated;
        
        private TopicEventBus() {
            // Initialize empty subscriber lists for each event type
            foreach (TopicEventType eventType in Enum.GetValues(typeof(TopicEventType))) {
                _subscribers.TryAdd(eventType, new List<Func<TopicEvent, Task>>());
            }
        }
        
        /// <summary>
        /// Terminates the topic event bus, clearing all subscribers and event history.
        /// </summary>
        public void Terminate() {
            if (_isTerminated) return;
            
            _logger?.LogInformation("[TopicEventBus] Terminating event bus");
            
            // Clear all subscribers
            foreach (var eventType in _subscribers.Keys.ToList()) {
                if (_subscribers.TryGetValue(eventType, out var handlers)) {
                    lock (handlers) {
                        handlers.Clear();
                    }
                }
            }
            
            // Clear event history
            while (_eventHistory.TryDequeue(out _)) { }
            
            _isTerminated = true;
            _logger?.LogInformation("[TopicEventBus] Event bus terminated");
        }
        
        /// <summary>
        /// Asynchronously terminates the topic event bus.
        /// </summary>
        public Task TerminateAsync(CancellationToken cancellationToken = default) {
            Terminate();
            return Task.CompletedTask;
        }

        public void Subscribe(TopicEventType eventType, Func<TopicEvent, Task> handler) {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _subscribers.AddOrUpdate(
                eventType,
                _ => new List<Func<TopicEvent, Task>> { handler },
                (_, handlers) => {
                    lock (handlers) {
                        handlers.Add(handler);
                    }
                    return handlers;
                });
        }

        public void Unsubscribe(TopicEventType eventType, Func<TopicEvent, Task> handler) {
            if (handler == null) return;

            if (_subscribers.TryGetValue(eventType, out var handlers)) {
                lock (handlers) {
                    handlers.Remove(handler);
                }
            }
        }

        public async Task PublishAsync(TopicEvent topicEvent) {
            if (topicEvent == null) throw new ArgumentNullException(nameof(topicEvent));

            // Add to bounded history
            _eventHistory.Enqueue(topicEvent);
            while (_eventHistory.Count > MaxHistorySize && _eventHistory.TryDequeue(out _)) { }

            if (_subscribers.TryGetValue(topicEvent.EventType, out var handlers)) {
                List<Func<TopicEvent, Task>> snapshot;
                lock (handlers) {
                    snapshot = handlers.ToList(); // copy for thread safety
                }

                var tasks = snapshot.Select(h => h(topicEvent));
                await Task.WhenAll(tasks);
            }
        }

        public Task PublishAsync(
            TopicEventType eventType,
            string topicName,
            string conversationId,
            object? data = null,
            string? correlationId = null) {
            var topicEvent = new TopicEvent(
                eventType,
                topicName,
                conversationId,
                data,
                correlationId ?? Guid.NewGuid().ToString()
            );

            return PublishAsync(topicEvent);
        }

        public IReadOnlyList<TopicEvent> GetEventHistory(string conversationId) =>
            _eventHistory.Where(e => e.ConversationId == conversationId).ToImmutableList();

        public IReadOnlyList<TopicEvent> GetEventHistory(string topicName, string conversationId) =>
            _eventHistory.Where(e => e.TopicName == topicName && e.ConversationId == conversationId).ToImmutableList();

        public IReadOnlyList<TopicEvent> GetCorrelatedEvents(string correlationId) =>
            _eventHistory.Where(e => e.CorrelationId == correlationId).ToImmutableList();
    }
}
