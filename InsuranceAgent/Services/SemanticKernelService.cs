using InsuranceAgent.Models;
using InsuranceAgent.Configuration;
using ConversaCore.Models;
using ConversaCore.Events;
using ConversaCore.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace InsuranceAgent.Services;

/// <summary>
/// Insurance-specific implementation of Semantic Kernel service.
/// Provides insurance-focused prompts and event triggers.
/// </summary>
public class InsuranceSemanticKernelService : ConversaCore.Services.SemanticKernelService {
    private readonly OpenAIConfiguration _openAiConfig;
    private readonly Kernel _kernel;
    private readonly ILogger<InsuranceSemanticKernelService> _logger;

    // Override base system prompt with insurance-specific one
    protected override string SystemPrompt => "You are a helpful insurance assistant. You help users with insurance-related questions, " +
        "guide them through forms and processes, and provide information about insurance products. " +
        "Be friendly, professional, and concise. If a user asks about starting a health questionnaire, " +
        "respond that you can help them get started. If they mention needing to speak with an agent, " +
        "acknowledge their request.";

    public InsuranceSemanticKernelService(
        Kernel kernel,
        IOptions<OpenAIConfiguration> openAiConfig,
        ILogger<InsuranceSemanticKernelService> logger)
        : base(kernel, logger)
    {
        _openAiConfig = openAiConfig.Value;
        _kernel = kernel;
        _logger = logger;
    }

    /// <summary>
    /// Override to handle ChatSessionState casting
    /// </summary>
    public new async Task<SemanticKernelResponse> ProcessMessageAsync(
        string userMessage,
        ChatSessionStateBase sessionState) {

        var insuranceSession = sessionState as ChatSessionState
                               ?? new ChatSessionState();

        return await ProcessMessageInternalAsync(userMessage, insuranceSession);
    }

    /// <summary>
    /// Override AI processing to use insurance-specific settings
    /// </summary>
    protected override async Task<SemanticKernelResponse> ProcessWithAIAsync(
        string userMessage,
        ChatSessionStateBase sessionState) {

        try {
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemPrompt);

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

            // Insurance-specific event analysis
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

            // Fall back to insurance-specific keyword processing
            return ProcessWithInsuranceFallback(userMessage, sessionState);
        }
    }

    /// <summary>
    /// Insurance-specific fallback processing
    /// </summary>
    private SemanticKernelResponse ProcessWithInsuranceFallback(
        string userMessage,
        ChatSessionStateBase sessionState) {

        var response = new SemanticKernelResponse {
            Content = $"🤖 I received your message: {userMessage}",
            IsAdaptiveCard = false,
            AdaptiveCardJson = null,
            Events = new List<ChatEvent>()
        };

        var lower = userMessage.ToLowerInvariant();

        // Insurance-specific keyword-based demo triggers
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
    /// Insurance-specific event analysis
    /// </summary>
    protected override List<ChatEvent> AnalyzeResponseForEvents(string aiResponse, string userMessage) {
        var events = new List<ChatEvent>();
        var responseLower = aiResponse.ToLowerInvariant();
        var userLower = userMessage.ToLowerInvariant();

        // Look for patterns that suggest specific insurance actions
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
}
