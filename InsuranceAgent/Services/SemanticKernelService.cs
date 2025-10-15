using InsuranceAgent.Models;
using InsuranceAgent.Configuration;
using ConversaCore.Services;
using ConversaCore.Models;
using ConversaCore.Events;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Services;

/// <summary>
/// Event-driven service wrapper around Semantic Kernel.
/// Emits events instead of returning raw responses.
/// </summary>
public class SemanticKernelService : ISemanticKernelService {
    private readonly Kernel _kernel;
    private readonly OpenAIConfiguration _openAiConfig;
    private readonly ILogger<SemanticKernelService> _logger;
    
    // === Events (outbound to HybridChatService) ===
    public event EventHandler<SemanticMessageEventArgs>? SemanticMessageReady;
    public event EventHandler<SemanticAdaptiveCardEventArgs>? SemanticAdaptiveCardReady;
    public event EventHandler<SemanticChatEventArgs>? SemanticChatEventRaised;
    public event EventHandler<SemanticTypingEventArgs>? SemanticTypingIndicatorChanged;

    public SemanticKernelService(
        Kernel kernel,
        IOptions<OpenAIConfiguration> openAiConfig,
        ILogger<SemanticKernelService> logger)
    {
        _kernel = kernel;
        _openAiConfig = openAiConfig.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes a user message using Semantic Kernel.
    /// Downcasts to ChatSessionState if possible.
    /// </summary>
    public async Task<SemanticKernelResponse> ProcessMessageAsync(
        string userMessage,
        ChatSessionStateBase sessionState) {

        var insuranceSession = sessionState as ChatSessionState
                               ?? new ChatSessionState();

        return await ProcessMessageInternalAsync(userMessage, insuranceSession);
    }

    /// <summary>
    /// Internal processing logic - uses OpenAI when available, falls back to keyword-based responses.
    /// </summary>
    private async Task<SemanticKernelResponse> ProcessMessageInternalAsync(
        string userMessage,
        ChatSessionState sessionState) {

        // Start typing indicator
        OnSemanticTypingIndicatorChanged(true);

        try {
            SemanticKernelResponse response;

            if (_openAiConfig.IsConfigured) {
                try {
                    var chatService = _kernel.GetRequiredService<IChatCompletionService>();
                    if (chatService != null) {
                        _logger.LogDebug("Processing message with OpenAI: {Message}", userMessage);
                        response = await ProcessWithOpenAIAsync(userMessage, sessionState);
                    } else {
                        _logger.LogDebug("No chat completion service available, using fallback");
                        response = ProcessWithKeywordFallback(userMessage, sessionState);
                    }
                } catch (InvalidOperationException) {
                    _logger.LogDebug("Chat completion service not configured, using fallback");
                    response = ProcessWithKeywordFallback(userMessage, sessionState);
                }
            } else {
                _logger.LogDebug("Processing message with keyword fallback: {Message}", userMessage);
                response = ProcessWithKeywordFallback(userMessage, sessionState);
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
    /// Process message using OpenAI/LLM
    /// </summary>
    private async Task<SemanticKernelResponse> ProcessWithOpenAIAsync(
        string userMessage,
        ChatSessionState sessionState) {
        
        try {
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(
                "You are a helpful insurance assistant. You help users with insurance-related questions, " +
                "guide them through forms and processes, and provide information about insurance products. " +
                "Be friendly, professional, and concise. If a user asks about starting a health questionnaire, " +
                "respond that you can help them get started. If they mention needing to speak with an agent, " +
                "acknowledge their request."
            );

            // Could add conversation history here if needed in the future

            chatHistory.AddUserMessage(userMessage);

            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                new PromptExecutionSettings()
                {
                    ExtensionData = new Dictionary<string, object>
                    {
                        ["max_tokens"] = _openAiConfig.MaxTokens,
                        ["temperature"] = _openAiConfig.Temperature
                    }
                }
            );

            var content = result.Content ?? "I'm sorry, I didn't understand that. Could you please rephrase?";
            
            // Analyze response for potential events
            var events = AnalyzeResponseForEvents(content, userMessage);

            return new SemanticKernelResponse {
                Content = content,
                IsAdaptiveCard = false,
                AdaptiveCardJson = null,
                Events = events
            };
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error processing message with OpenAI");
            
            // Fall back to keyword processing
            return ProcessWithKeywordFallback(userMessage, sessionState);
        }
    }

    /// <summary>
    /// Fallback keyword-based processing when OpenAI is not available
    /// </summary>
    private SemanticKernelResponse ProcessWithKeywordFallback(
        string userMessage,
        ChatSessionState sessionState) {
        
        var response = new SemanticKernelResponse {
            Content = $"🤖 I received your message: {userMessage}",
            IsAdaptiveCard = false,
            AdaptiveCardJson = null,
            Events = new List<ChatEvent>()
        };

        var lower = userMessage.ToLowerInvariant();

        // Simple keyword-based demo triggers
        if (lower.Contains("questionnaire") || lower.Contains("health") || lower.Contains("questions")) {
            response.Content = "I can help you get started with a health questionnaire. Let me guide you through it.";
            response.Events.Add(new ChatEvent { Type = "startHealthQuestionnaire" });
        }
        else if (lower.Contains("consent") || lower.Contains("agree") || lower.Contains("accept")) {
            response.Content = "Thank you for your consent. I'll process that for you.";
            response.Events.Add(new ChatEvent { Type = "userTCPAAuthorizationReceived" });
        }
        else if (lower.Contains("agent") || lower.Contains("person") || lower.Contains("human")) {
            response.Content = "I understand you'd like to speak with a human agent. Let me help connect you.";
            response.Events.Add(new ChatEvent { Type = "requestConsent" });
        }
        else if (lower.Contains("hello") || lower.Contains("hi") || lower.Contains("hey")) {
            response.Content = "Hello! I'm here to help you with your insurance needs. What can I assist you with today?";
        }
        else if (lower.Contains("help")) {
            response.Content = "I'm here to help! I can assist you with insurance questions, guide you through forms, or connect you with an agent. What would you like to do?";
        }

        return response;
    }

    /// <summary>
    /// Analyze LLM response to determine if any events should be triggered
    /// </summary>
    private List<ChatEvent> AnalyzeResponseForEvents(string aiResponse, string userMessage) {
        var events = new List<ChatEvent>();
        var responseLower = aiResponse.ToLowerInvariant();
        var userLower = userMessage.ToLowerInvariant();

        // Look for patterns that suggest specific actions
        if ((responseLower.Contains("questionnaire") || responseLower.Contains("health questions")) && 
            (userLower.Contains("health") || userLower.Contains("questions"))) {
            events.Add(new ChatEvent { Type = "startHealthQuestionnaire" });
        }
        
        if (responseLower.Contains("agent") || responseLower.Contains("human") || 
            userLower.Contains("speak") && userLower.Contains("person")) {
            events.Add(new ChatEvent { Type = "requestConsent" });
        }

        return events;
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
