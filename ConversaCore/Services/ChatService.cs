using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.Topics;
using Microsoft.SemanticKernel;

namespace ConversaCore.Services; 
/// <summary>
/// Interface for a service that processes chat messages using topics
/// </summary>
public interface IChatService {
    Task<ChatResponse> ProcessMessageAsync(string message, string conversationId, string userId);
}

/// <summary>
/// Default implementation of the chat service
/// </summary>
public class ChatService : IChatService {
    private readonly Kernel _kernel;
    private readonly TopicRegistry _topicRegistry;
    private readonly IConversationContext _context;

    public ChatService(Kernel kernel, TopicRegistry topicRegistry) {
        // _context should be injected from HybridChatService
        throw new InvalidOperationException("Use the constructor that accepts IConversationContext");
    }

    public ChatService(Kernel kernel, TopicRegistry topicRegistry, IConversationContext context) {
        _kernel = kernel;
        _topicRegistry = topicRegistry;
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ChatResponse> ProcessMessageAsync(string message, string conversationId, string userId) {
    var context = _context;

        // Log the user message as an event
        await TopicEventBus.Instance.PublishAsync(
            TopicEventType.UserMessageReceived,
            context.CurrentTopicName ?? "None",
            conversationId,
            message);

        // Pick best topic for this message
        var (topic, confidence) = await _topicRegistry.FindBestTopicAsync(message, context);

        if (topic == null) {
            return CreateErrorResponse("No topic could handle this message.", conversationId);
        }

        // Update context if topic switched
        if (context.CurrentTopicName != topic.Name) {
            context.SetCurrentTopic(topic.Name);
        }

        try {
            var result = await topic.ProcessMessageAsync(message);

            // Handle topic completion → chain to next topic
            if (result.IsCompleted && !string.IsNullOrEmpty(result.NextTopicName)) {
                context.AddTopicToChain(result.NextTopicName);
                await TopicEventBus.Instance.PublishAsync(
                    TopicEventType.TopicTransitionRequested,
                    topic.Name,
                    conversationId,
                    result.NextTopicName);
            }

            // Publish bot message
            string responseText = result.Response ?? string.Empty;
            await TopicEventBus.Instance.PublishAsync(
                TopicEventType.BotMessageSent,
                topic.Name,
                conversationId,
                responseText);

            // Publish adaptive card event if present
            if (!string.IsNullOrEmpty(result.AdaptiveCardJson)) {
                await TopicEventBus.Instance.PublishAsync(
                    TopicEventType.AdaptiveCardDisplayed,
                    topic.Name,
                    conversationId,
                    result.AdaptiveCardJson);
            }

            return new ChatResponse {
                Message = responseText,
                AdaptiveCardJson = result.AdaptiveCardJson ?? string.Empty,
                TopicName = topic.Name
            };
        } catch (Exception ex) {
            Console.WriteLine($"Error processing message: {ex}");
            return CreateErrorResponse($"An error occurred: {ex.Message}", conversationId);
        }
    }

    private IConversationContext GetOrCreateConversationContext(string conversationId, string userId) {
        // Removed: context creation logic. Context is now injected.
        return _context;
    }

    private ChatResponse CreateErrorResponse(string message, string conversationId) {
        Task.Run(async () => await TopicEventBus.Instance.PublishAsync(
            TopicEventType.BotMessageSent,
            "Error",
            conversationId,
            message));

        return new ChatResponse {
            Message = message,
            TopicName = "Error"
        };
    }
}
