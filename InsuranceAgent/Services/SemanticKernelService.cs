using InsuranceAgent.Models;
using ConversaCore.Services;
using ConversaCore.Models;
using ConversaCore.Events;

namespace InsuranceAgent.Services;

/// <summary>
/// Event-driven service wrapper around Semantic Kernel.
/// Emits events instead of returning raw responses.
/// </summary>
public class SemanticKernelService : ISemanticKernelService {
    // === Events (outbound to HybridChatService) ===
    public event EventHandler<SemanticMessageEventArgs>? SemanticMessageReady;
    public event EventHandler<SemanticAdaptiveCardEventArgs>? SemanticAdaptiveCardReady;
    public event EventHandler<SemanticChatEventArgs>? SemanticChatEventRaised;
    public event EventHandler<SemanticTypingEventArgs>? SemanticTypingIndicatorChanged;

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
    /// Internal placeholder logic (currently keyword-based).
    /// </summary>
    private async Task<SemanticKernelResponse> ProcessMessageInternalAsync(
        string userMessage,
        ChatSessionState sessionState) {

        // Start typing indicator
        OnSemanticTypingIndicatorChanged(true);

        try {
            var response = new SemanticKernelResponse {
                Content = $"🤖 I received your message: {userMessage}",
                IsAdaptiveCard = false,
                AdaptiveCardJson = null,
                Events = new List<ChatEvent>()
            };

            var lower = userMessage.ToLowerInvariant();

            // Simple keyword-based demo triggers
            if (lower.Contains("questionnaire") || lower.Contains("health") || lower.Contains("questions")) {
                response.Events.Add(new ChatEvent { Type = "startHealthQuestionnaire" });
            }
            else if (lower.Contains("consent") || lower.Contains("agree") || lower.Contains("accept")) {
                response.Events.Add(new ChatEvent { Type = "userTCPAAuthorizationReceived" });
            }
            else if (lower.Contains("agent") || lower.Contains("person") || lower.Contains("human")) {
                response.Events.Add(new ChatEvent { Type = "requestConsent" });
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
