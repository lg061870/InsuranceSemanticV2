using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;

namespace ConversaCore.Topics;

/// <summary>
/// Interface for the topic manager service.
/// </summary>
public interface ITopicManager {
    /// <summary>
    /// Processes a user message through the available topics.
    /// </summary>
    Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight topic manager for routing user messages to the right topic.
/// Responsible for coordinating active topics, selecting the best topic,
/// and ensuring the TopicWorkflowContext flows across.
/// </summary>
public class TopicManager : ITopicManager {
    private readonly IEnumerable<ITopic> _topics;
    private readonly IConversationContext _context;
    private readonly TopicWorkflowContext _wfContext;
    private readonly ILogger<TopicManager> _logger;

    public TopicManager(
        IEnumerable<ITopic> topics,
        IConversationContext context,
        TopicWorkflowContext wfContext,
        ILogger<TopicManager> logger) {
        _topics = topics ?? throw new ArgumentNullException(nameof(topics));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _wfContext = wfContext ?? throw new ArgumentNullException(nameof(wfContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation("TopicManager initialized with {TopicCount} topics", _topics.Count());
    }

    /// <inheritdoc/>
    public async Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Processing message: {Message}",
            string.IsNullOrEmpty(message) ? "<empty>" : message[..Math.Min(30, message.Length)]);

        // 1. If there’s an active topic, try that first
        if (!string.IsNullOrEmpty(_context.CurrentTopicName)) {
            var activeTopic = _topics.FirstOrDefault(t => t.Name == _context.CurrentTopicName);
            if (activeTopic != null) {
                var result = await activeTopic.ProcessMessageAsync(message, cancellationToken);

                // ?? ensure wfContext crosses over
                result.wfContext = _wfContext;

                if (result.IsHandled) {
                    _logger.LogInformation("Message handled by active topic: {TopicName}", activeTopic.Name);
                    return result;
                }
            }
        }

        // 2. Otherwise, pick the best topic by confidence
        foreach (var topic in _topics.OrderByDescending(t => t.Priority)) {
            try {
                float confidence = await topic.CanHandleAsync(message, cancellationToken);
                if (confidence > 0.5f) {
                    var result = await topic.ProcessMessageAsync(message, cancellationToken);

                    // ?? attach workflow context
                    result.wfContext = _wfContext;

                    if (result.IsHandled) {
                        _logger.LogInformation("Message handled by topic {TopicName} (confidence {Confidence})",
                            topic.Name, confidence);

                        if (result.KeepActive)
                            _context.SetCurrentTopic(topic.Name);

                        return result;
                    }
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Error processing message with topic {TopicName}", topic.Name);
            }
        }

        // 3. Fallback: try a default topic
        var defaultTopic = _topics.FirstOrDefault(t => t.Name == "Default")
                         ?? _topics.OrderBy(t => t.Priority).FirstOrDefault();

        if (defaultTopic != null) {
            _logger.LogInformation("Fallback to default topic: {TopicName}", defaultTopic.Name);
            var result = await defaultTopic.ProcessMessageAsync(message, cancellationToken);

            // ?? attach wfContext
            result.wfContext = _wfContext;

            return result;
        }

        // 4. Last resort: generic response
        _logger.LogWarning("No topic handled the message: {Message}", message);

        return TopicResult.CreateResponse(
            "I'm not sure how to respond to that. Could you rephrase?",
            _wfContext,
            requiresInput: true
        );
    }
}
