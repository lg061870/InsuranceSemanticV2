using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConversaCore.Core;

namespace ConversaCore.Topics {
    /// <summary>
    /// Simple registry for all available topics in the system.
    /// Implements ITerminable to support proper cleanup of topics during shutdown or reset.
    /// </summary>
    public class TopicRegistry : ITerminable {
        private readonly List<ITopic> _topics = new();
        private readonly ILogger<TopicRegistry>? _logger;
        private bool _isTerminated = false;
        
        /// <summary>
        /// Gets whether this registry has been terminated.
        /// </summary>
        public bool IsTerminated => _isTerminated;
        
        /// <summary>
        /// Initializes a new instance of the TopicRegistry class.
        /// </summary>
        public TopicRegistry(ILogger<TopicRegistry>? logger = null) {
            _logger = logger;
        }

        /// <summary>
        /// Registers a topic with the registry.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the registry has been terminated.</exception>
        public void RegisterTopic(ITopic topic) {
            if (topic == null) throw new ArgumentNullException(nameof(topic));
            if (_isTerminated) throw new InvalidOperationException("Cannot register a topic with a terminated registry.");

            // Check for terminated topics
            if (topic is ITerminable terminable && terminable.IsTerminated)
            {
                _logger?.LogWarning("[TopicRegistry] Attempted to register terminated topic: {TopicName}", topic.Name);
                throw new InvalidOperationException($"Cannot register terminated topic '{topic.Name}'.");
            }

            // Avoid duplicate registrations
            if (!_topics.Any(t => t.Name.Equals(topic.Name, StringComparison.OrdinalIgnoreCase))) {
                _topics.Add(topic);
                _logger?.LogDebug("[TopicRegistry] Registered topic: {TopicName}", topic.Name);
            }
            else {
                _logger?.LogDebug("[TopicRegistry] Topic already registered: {TopicName}", topic.Name);
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
        /// Resets the registry and all terminable topics within it.
        /// This will properly clean up resources and prepare topics for a new conversation.
        /// </summary>
        public void Reset()
        {
            _logger?.LogInformation("[TopicRegistry] Resetting registry with {Count} topics", _topics.Count);
            
            // If we're terminated, don't allow reset
            if (_isTerminated)
            {
                _logger?.LogWarning("[TopicRegistry] Cannot reset terminated registry");
                return;
            }
            
            // First, reset all topics that are derived from TopicFlow or implement ITerminable
            foreach (var topic in _topics)
            {
                if (topic is ConversaCore.TopicFlow.TopicFlow topicFlow)
                {
                    _logger?.LogInformation("[TopicRegistry] Resetting TopicFlow topic: {TopicName}", topic.Name);
                    try
                    {
                        topicFlow.Reset();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[TopicRegistry] Error resetting topic {TopicName}", topic.Name);
                    }
                }
                else if (topic is ITerminable terminable && !terminable.IsTerminated)
                {
                    _logger?.LogInformation("[TopicRegistry] Terminating and re-registering ITerminable topic: {TopicName}", topic.Name);
                    try
                    {
                        terminable.Terminate();
                        // Note: We don't recreate the topic here - that's the responsibility of the 
                        // service that initially registered it, typically via DI scoped services
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[TopicRegistry] Error terminating topic {TopicName}", topic.Name);
                    }
                }
            }
            
            // Now clear the registry to remove any terminated topics
            Clear();
            
            _logger?.LogInformation("[TopicRegistry] Reset completed - registry is now empty");
        }
        
        /// <summary>
        /// Terminates the registry and all terminable topics within it.
        /// This will properly clean up resources and prevent memory leaks.
        /// </summary>
        public void Terminate()
        {
            if (_isTerminated) return; // Already terminated
            
            _logger?.LogInformation("[TopicRegistry] Terminating registry with {Count} topics", _topics.Count);
            
            // Terminate all ITerminable topics
            foreach (var topic in _topics)
            {
                if (topic is ITerminable terminable && !terminable.IsTerminated)
                {
                    _logger?.LogDebug("[TopicRegistry] Terminating topic: {TopicName}", topic.Name);
                    try
                    {
                        terminable.Terminate();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[TopicRegistry] Error terminating topic {TopicName}", topic.Name);
                    }
                }
            }
            
            // Clear all topics
            Clear();
            
            // Mark as terminated
            _isTerminated = true;
            
            _logger?.LogInformation("[TopicRegistry] Registry terminated");
        }
        
        /// <summary>
        /// Asynchronously terminates the registry and all terminable topics within it.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the termination process.</param>
        public async Task TerminateAsync(CancellationToken cancellationToken = default)
        {
            if (_isTerminated) return; // Already terminated
            
            _logger?.LogInformation("[TopicRegistry] Asynchronously terminating registry with {Count} topics", _topics.Count);
            
            // Collect all terminate tasks
            var terminateTasks = new List<Task>();
            
            foreach (var topic in _topics)
            {
                if (topic is ITerminable terminable && !terminable.IsTerminated)
                {
                    _logger?.LogDebug("[TopicRegistry] Asynchronously terminating topic: {TopicName}", topic.Name);
                    try
                    {
                        terminateTasks.Add(terminable.TerminateAsync(cancellationToken));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[TopicRegistry] Error starting async termination for topic {TopicName}", topic.Name);
                    }
                }
            }
            
            if (terminateTasks.Count > 0)
            {
                try
                {
                    // Use WhenAny with timeout to avoid hanging if a task never completes
                    var completedTask = await Task.WhenAny(
                        Task.WhenAll(terminateTasks),
                        Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)
                    );
                    
                    if (completedTask.IsCompleted && !completedTask.IsFaulted)
                    {
                        _logger?.LogDebug("[TopicRegistry] All topics terminated asynchronously");
                    }
                    else
                    {
                        _logger?.LogWarning("[TopicRegistry] Timed out waiting for topics to terminate");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[TopicRegistry] Error during async termination of topics");
                }
            }
            
            // Perform synchronous termination to ensure everything is cleaned up
            Terminate();
            
            _logger?.LogInformation("[TopicRegistry] Registry terminated asynchronously");
        }

        /// <summary>
        /// Finds the best topic to handle a given message.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the registry has been terminated.</exception>
        public async Task<(ITopic? Topic, float Confidence)> FindBestTopicAsync(
            string message,
            Context.IConversationContext context,
            CancellationToken cancellationToken = default) {
            
            if (_isTerminated)
                throw new InvalidOperationException("Cannot find topics in a terminated registry.");
                
            if (!_topics.Any())
                return (null, 0f);

            ITopic? bestTopic = null;
            float bestConfidence = 0f;

            // Filter out any terminated topics before processing
            var activeTopics = _topics.Where(t => !(t is ITerminable terminable) || !terminable.IsTerminated)
                                     .OrderByDescending(t => t.Priority)
                                     .ToList();

            // Ensure message is not null for safety
            message = message ?? string.Empty;
            
            _logger?.LogDebug("[TopicRegistry] Finding best topic among {Count} active topics for message: {Message}", 
                              activeTopics.Count, message.Length > 30 ? message.Substring(0, 30) + "..." : message);

            foreach (var topic in activeTopics) {
                try {
                    var confidence = await topic.CanHandleAsync(message, cancellationToken);
                    _logger?.LogTrace("[TopicRegistry] Topic {TopicName} confidence: {Confidence}", topic.Name, confidence);
                    
                    if (confidence > bestConfidence) {
                        bestConfidence = confidence;
                        bestTopic = topic;
                    }
                } catch (Exception ex) {
                    // Skip any failing topics
                    _logger?.LogWarning(ex, "[TopicRegistry] Topic {TopicName} threw exception during CanHandleAsync", topic.Name);
                }
            }

            // fallback to Default topic if no confident match
            if (bestTopic == null || bestConfidence < 0.3f) {
                _logger?.LogDebug("[TopicRegistry] No confident match, falling back to Default topic");
                bestTopic = GetTopic("Default");
                bestConfidence = 1.0f;
            }

            _logger?.LogInformation("[TopicRegistry] Best topic: {TopicName} with confidence {Confidence}", 
                                   bestTopic?.Name ?? "None", bestConfidence);
            
            return (bestTopic, bestConfidence);
        }
    }
}
