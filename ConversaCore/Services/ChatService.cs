using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.Topics;
using ConversaCore.TopicFlow;
using Microsoft.SemanticKernel;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConversaCore.Services; 
/// <summary>
/// Interface for a service that processes chat messages using topics
/// </summary>
public interface IChatService {
    /// <summary>
    /// Processes a chat message and returns a response.
    /// </summary>
    Task<ChatResponse> ProcessMessageAsync(string message, string conversationId, string userId);
    
    /// <summary>
    /// Resets the chat service, clearing any conversation state and terminating active topics.
    /// </summary>
    Task<bool> ResetAsync();
}

/// <summary>
/// Default implementation of the chat service
/// </summary>
public class ChatService : IChatService {
    private readonly Kernel _kernel;
    private readonly TopicRegistry _topicRegistry;
    private readonly IConversationContext _context;
    private readonly ILogger<ChatService>? _logger;
    private readonly IServiceProvider? _serviceProvider;

    public ChatService(Kernel kernel, TopicRegistry topicRegistry) {
        // _context should be injected from HybridChatService
        throw new InvalidOperationException("Use the constructor that accepts IConversationContext");
    }

    public ChatService(Kernel kernel, TopicRegistry topicRegistry, IConversationContext context) {
        _kernel = kernel;
        _topicRegistry = topicRegistry;
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }
    
    public ChatService(
        Kernel kernel, 
        TopicRegistry topicRegistry, 
        IConversationContext context,
        ILogger<ChatService>? logger = null,
        IServiceProvider? serviceProvider = null) {
        _kernel = kernel;
        _topicRegistry = topicRegistry;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    
    /// <summary>
    /// Resets the chat service, clearing any conversation state and terminating active topics.
    /// </summary>
    /// <returns>True if reset was successful, false otherwise.</returns>
    public async Task<bool> ResetAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        _logger?.LogInformation("[ChatService] Beginning reset of chat service");
        
        try
        {
            // First, terminate and reset the context which is scoped to this conversation
            await _context.TerminateAsync();
            _logger?.LogInformation("[ChatService] Conversation context terminated and reset");
            
            // Reset the topic registry (which is a singleton) if we're allowed to
            // This will terminate all topics and clear the registry
            if (_topicRegistry != null)
            {
                _topicRegistry.Reset();
                _logger?.LogInformation("[ChatService] Topic registry reset");
            }
            
            // If we have access to the service provider, use it to reset all components
            if (_serviceProvider != null)
            {
                _serviceProvider.ResetConversaCore();
                _logger?.LogInformation("[ChatService] Full ConversaCore reset completed via service provider");
            }
            
            // Notify event bus about the reset
            await TopicEventBus.Instance.PublishAsync(
                TopicEventType.ConversationReset,
                "System",
                _context.ConversationId ?? "unknown",
                "Conversation reset completed");
                
            stopwatch.Stop();
            _logger?.LogInformation("[ChatService] Reset completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "[ChatService] Error during reset operation (elapsed time: {ElapsedMs}ms)", stopwatch.ElapsedMilliseconds);
            return false;
        }
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
                TopicName = topic.Name,
                wfContext = new TopicWorkflowContext()
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
            TopicName = "Error",
            wfContext = new TopicWorkflowContext()
        };
    }
}
