using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.Services;
using ConversaCore.Topics;
using InsuranceAgent.Models;

namespace InsuranceAgent.Services;

/// <summary>
/// Orchestrates between:
/// 1. InsuranceAgentService (deterministic workflow / TopicFlow)
/// 2. SemanticKernelService (AI generative reasoning)
///
/// Provides a normalized event-driven interface for the ChatWindow.
/// </summary>
public class HybridChatService {
    private readonly ILogger<HybridChatService> _logger;
    private readonly InsuranceAgentService _agentService;
    private readonly ISemanticKernelService _semanticKernelService;
    private readonly TopicRegistry _topicRegistry;
    private readonly IConversationContext _context;

    public IConversationContext Context => _context;
    public ConversaCore.TopicFlow.TopicWorkflowContext WorkflowContext { get; }

    // === Lifecycle ===
    public event Action<ChatSessionState, ITopic>? OnConversationStart;

    public HybridChatService(
        ILogger<HybridChatService> logger,
        InsuranceAgentService agentService,
        ISemanticKernelService semanticKernelService,
        TopicRegistry topicRegistry,
        IConversationContext context) {
        _logger = logger;
        _agentService = agentService;
        _semanticKernelService = semanticKernelService;
        _topicRegistry = topicRegistry;
        _context = context;

        WorkflowContext = new ConversaCore.TopicFlow.TopicWorkflowContext();

        // ===== Subscribe to InsuranceAgent events =====
        _agentService.ActivityMessageReady += (s, e) => {
            _logger.LogInformation("[HybridChatService] Forwarding ActivityMessageReady -> HybridBotMessageReady: {Content}", e.Message.Content);
            OnHybridBotMessageReady(e.Message);
        };

        _agentService.ActivityAdaptiveCardReady += (s, e) =>
    OnHybridAdaptiveCardReady(e.CardJson, e.CardId, e.RenderMode);

        _agentService.ActivityCompleted += (s, e) =>
            OnHybridChatEventRaised(new ChatEvent { Type = "ActivityCompleted", Payload = e.Context });

        _agentService.TopicLifecycleChanged += (s, e) =>
            OnHybridChatEventRaised(new ChatEvent { Type = "TopicLifecycleChanged", Payload = e });
        _agentService.ActivityLifecycleChanged += (s, e) =>
            OnHybridChatEventRaised(new ChatEvent { Type = "ActivityLifecycleChanged", Payload = e });
        _agentService.TopicInserted += (s, e) =>
            OnHybridChatEventRaised(new ChatEvent { Type = "TopicInserted", Payload = e });

        _agentService.MatchingTopicNotFound += async (s, e) => {
            _logger.LogInformation("No topic could process '{Message}', escalating to Semantic Kernel", e.UserMessage);
            await _semanticKernelService.ProcessMessageAsync(e.UserMessage, new ChatSessionState());
        };

        // ===== Subscribe to SemanticKernel events =====
        _semanticKernelService.SemanticMessageReady += (s, e) => OnHybridBotMessageReady(e.Message);
        //_semanticKernelService.SemanticAdaptiveCardReady += (s, e) =>
        //    OnHybridAdaptiveCardReady(e.CardJson, Guid.NewGuid().ToString(), RenderMode.Append);
        _semanticKernelService.SemanticChatEventRaised += (s, e) => OnHybridChatEventRaised(e.ChatEvent);

        if (_semanticKernelService is ISemanticKernelTyping skTyping) {
            skTyping.SemanticTypingIndicatorChanged += (s, e) =>
                OnHybridTypingIndicatorChanged(e.IsTyping);
        }

        _logger.LogInformation("HybridChatService initialized with Agent + SemanticKernel orchestration");
    }

    // === Conversation start ===
    public void StartConversation(ChatSessionState sessionState) {
        var topic = _topicRegistry.GetTopic("ConversationStart");
        if (topic != null) {
            // Let the agent handle initialization + wiring
            _ = _agentService.StartConversationAsync(sessionState);
        }
    }


    // === ChatWindow → Hybrid ===
    public void HandleUserMessage(object? sender, MessageEventArgs e) {
        _ = HandleUserMessageAsync(e);
    }

    private async Task HandleUserMessageAsync(MessageEventArgs e) {
        try {
            _logger.LogInformation("[HybridChatService] UserMessageEntered: {Content}", e.Content);
            await _agentService.ProcessUserMessageAsync(e.Content, CancellationToken.None);
            TrackMessageInContext("user", e.Content);
        } catch (Exception ex) {
            _logger.LogError(ex, "Error in HandleUserMessageAsync");
        }
    }

    public void HandleBotMessage(object? sender, ChatWindowBotMessageEventArgs e) {
        _logger.LogInformation("[HybridChatService] BotMessageRendered: {Content}", e.Content);
        TrackMessageInContext("assistant", e.Content);

    }

    public void HandleCardSubmit(object? sender, ChatCardSubmitEventArgs e) {
        _ = HandleCardSubmitAsync(e);
    }

    private async Task HandleCardSubmitAsync(ChatCardSubmitEventArgs e) {
        try {
            _logger.LogInformation("[HybridChatService] AdaptiveCardSubmitted: {Keys}", string.Join(",", e.Data.Keys));
            await _agentService.HandleCardSubmitAsync(e.Data, CancellationToken.None);
        } catch (Exception ex) {
            _logger.LogError(ex, "Error in HandleCardSubmitAsync");
        }
    }

    public void HandleCardAction(object? sender, ChatWinowCardActionEventArgs e) {
        _logger.LogInformation("[HybridChatService] CardActionInvoked: {ActionId}", e.ActionId);
        TrackMessageInContext("user_action", e.ActionId);
    }

    // === Context ===
    private void TrackMessageInContext(string role, string content) {
        if (string.IsNullOrEmpty(content)) return;

        var messages = _context.GetValue<List<(string Role, string Content)>>("Messages")
                       ?? new List<(string, string)>();

        messages.Add((role, content));
        _context.SetValue("Messages", messages);
    }

    // === Hybrid → ChatWindow Events ===
    public event EventHandler<HybridBotMessageEventArgs>? HybridBotMessageReady;
    public event EventHandler<HybridAdaptiveCardEventArgs>? HybridAdaptiveCardReady;
    public event EventHandler<HybridTypingIndicatorEventArgs>? HybridTypingIndicatorChanged;
    public event EventHandler<HybridSuggestionsEventArgs>? HybridSuggestionsUpdated;
    public event EventHandler<HybridChatEventArgs>? HybridChatEventRaised;

    // === Raise methods ===
    protected virtual void OnHybridBotMessageReady(ChatMessage message) {
        _logger.LogInformation("[HybridChatService] OnHybridBotMessageReady fired: {Content}", message.Content);
        HybridBotMessageReady?.Invoke(this, new HybridBotMessageEventArgs(message));
    }

    protected virtual void OnHybridAdaptiveCardReady(string cardJson, string cardId, RenderMode mode)
     => HybridAdaptiveCardReady?.Invoke(this,
         new HybridAdaptiveCardEventArgs(cardJson, cardId, mode));

    protected virtual void OnHybridTypingIndicatorChanged(bool isTyping)
        => HybridTypingIndicatorChanged?.Invoke(this, new HybridTypingIndicatorEventArgs(isTyping));

    protected virtual void OnHybridSuggestionsUpdated(IEnumerable<string> suggestions)
        => HybridSuggestionsUpdated?.Invoke(this, new HybridSuggestionsEventArgs(suggestions));

    protected virtual void OnHybridChatEventRaised(ChatEvent chatEvent)
        => HybridChatEventRaised?.Invoke(this, new HybridChatEventArgs(chatEvent));
}
