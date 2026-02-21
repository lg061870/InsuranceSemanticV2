using ConversaCore.Models;
using ConversaCore.Events;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using ConversaCore.Interfaces;

namespace ConversaCore.Services;

/// <summary>
/// Base event-driven service wrapper around Semantic Kernel.
/// Provides generic AI chat processing with configurable system prompts.
/// Domain-specific implementations should inherit and customize prompts/events.
/// </summary>
public class SemanticKernelService : ISemanticKernelService {
    private readonly Kernel _kernel;
    private readonly ILogger<SemanticKernelService> _logger;

    // Configuration for prompts and behavior
    protected virtual string SystemPrompt => "You are a helpful assistant. Be friendly and professional.";

    // === Events (outbound to consumers) ===
    public event EventHandler<SemanticMessageEventArgs>? SemanticMessageReady;
    public event EventHandler<SemanticAdaptiveCardEventArgs>? SemanticAdaptiveCardReady;
    public event EventHandler<SemanticChatEventArgs>? SemanticChatEventRaised;
    public event EventHandler<SemanticTypingEventArgs>? SemanticTypingIndicatorChanged;

    public SemanticKernelService(
        Kernel kernel,
        ILogger<SemanticKernelService> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    /// <summary>
    /// Processes a user message using Semantic Kernel.
    /// </summary>
    public async Task<SemanticKernelResponse> ProcessMessageAsync(
        string userMessage,
        ChatSessionStateBase sessionState) {

        return await ProcessMessageInternalAsync(userMessage, sessionState);
    }

    /// <summary>
    /// Internal processing logic - uses OpenAI when available, falls back to basic responses.
    /// </summary>
    protected virtual async Task<SemanticKernelResponse> ProcessMessageInternalAsync(
        string userMessage,
        ChatSessionStateBase sessionState) {

        // Start typing indicator
        OnSemanticTypingIndicatorChanged(true);

        try {
            SemanticKernelResponse response;

            try {
                var chatService = _kernel.GetRequiredService<IChatCompletionService>();
                if (chatService != null) {
                    _logger.LogDebug("Processing message with AI: {Message}", userMessage);
                    response = await ProcessWithAIAsync(userMessage, sessionState);
                } else {
                    _logger.LogDebug("No chat completion service available, using fallback");
                    response = ProcessWithFallback(userMessage, sessionState);
                }
            } catch (InvalidOperationException) {
                _logger.LogDebug("Chat completion service not configured, using fallback");
                response = ProcessWithFallback(userMessage, sessionState);
            }

            // === Raise events ===
            if (!string.IsNullOrEmpty(response.Content)) {
                OnSemanticMessageReady(new ChatMessage {
                    Content = response.Content,
                    IsFromUser = false,
                    Timestamp = DateTime.Now
                });
            }

            if (response.IsAdaptiveCard && !string.IsNullOrEmpty(response.AdaptiveCardJson)) {
                OnSemanticAdaptiveCardReady(response.AdaptiveCardJson!);
            }

            if (response.Events?.Any() == true) {
                foreach (var evt in response.Events)
                    OnSemanticChatEventRaised(evt);
            }

            return response;
        } finally {
            // Always end typing indicator
            OnSemanticTypingIndicatorChanged(false);
        }
    }

    /// <summary>
    /// Process message using AI/LLM with configurable system prompt
    /// </summary>
    protected virtual async Task<SemanticKernelResponse> ProcessWithAIAsync(
        string userMessage,
        ChatSessionStateBase sessionState) {

        try {
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemPrompt);

            // Could add conversation history here if needed in the future

            chatHistory.AddUserMessage(userMessage);

            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                new PromptExecutionSettings()
                {
                    ExtensionData = new Dictionary<string, object>
                    {
                        ["max_tokens"] = 1000,
                        ["temperature"] = 0.7
                    }
                }
            );

            var content = result.Content ?? "I'm sorry, I didn't understand that. Could you please rephrase?";

            // Allow subclasses to analyze response for events
            var events = AnalyzeResponseForEvents(content, userMessage);

            return new SemanticKernelResponse {
                Content = content,
                IsAdaptiveCard = false,
                AdaptiveCardJson = null,
                Events = events
            };
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error processing message with AI");

            // Fall back to basic processing
            return ProcessWithFallback(userMessage, sessionState);
        }
    }

    /// <summary>
    /// Basic fallback processing when AI is not available
    /// </summary>
    protected virtual SemanticKernelResponse ProcessWithFallback(
        string userMessage,
        ChatSessionStateBase sessionState) {

        var response = new SemanticKernelResponse {
            Content = $"🤖 I received your message: {userMessage}",
            IsAdaptiveCard = false,
            AdaptiveCardJson = null,
            Events = new List<ChatEvent>()
        };

        // Basic greeting responses
        var lower = userMessage.ToLowerInvariant();
        if (lower.Contains("hello") || lower.Contains("hi") || lower.Contains("hey")) {
            response.Content = "Hello! How can I help you today?";
        }
        else if (lower.Contains("help")) {
            response.Content = "I'm here to help! What would you like to know?";
        }

        return response;
    }

    /// <summary>
    /// Analyze AI response to determine if any events should be triggered.
    /// Subclasses can override for domain-specific event detection.
    /// </summary>
    protected virtual List<ChatEvent> AnalyzeResponseForEvents(string aiResponse, string userMessage) {
        // Base implementation: no events
        return new List<ChatEvent>();
    }

    // === Protected Raise Methods ===
    protected virtual void OnSemanticMessageReady(ChatMessage message)
        => SemanticMessageReady?.Invoke(this, new SemanticMessageEventArgs(message));

    protected virtual void OnSemanticAdaptiveCardReady(string cardJson)
        => SemanticAdaptiveCardReady?.Invoke(this, new SemanticAdaptiveCardEventArgs(cardJson));

    protected virtual void OnSemanticChatEventRaised(ChatEvent chatEvent)
        => SemanticChatEventRaised?.Invoke(this, new SemanticChatEventArgs(chatEvent));

    protected virtual void OnSemanticTypingIndicatorChanged(bool isTyping)
        => SemanticTypingIndicatorChanged?.Invoke(this, new SemanticTypingEventArgs(isTyping));
}