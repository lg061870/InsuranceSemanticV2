using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using InsuranceAgent.Models;

namespace InsuranceAgent.Services; 
/// <summary>
/// 🔄 HybridChatServiceV2
/// Simplified bridge between the deterministic InsuranceAgentService (runtime)
/// and the chat UI (CustomChatWindowV2).
///
/// Responsibilities:
///  - Subscribes to InsuranceAgentService events.
///  - Forwards unified Hybrid* events to the UI.
///  - Keeps conversation context reference for continuity.
///  - Does NOT handle SemanticKernel, domain initialization, or business logic.
/// </summary>
public class HybridChatServiceV2 {
    private readonly ILogger<HybridChatServiceV2> _logger;
    private readonly InsuranceAgentService _agentService;
    private readonly IConversationContext _context;

    public IConversationContext Context => _context;

    // === Lifecycle ===
    public event EventHandler<ConversationResetEventArgs>? OnConversationReset;
    public event EventHandler<PromptInputStateChangedEventArgs>? PromptInputStateChanged;

    // === Hybrid UI Events ===
    public event EventHandler<HybridBotMessageEventArgs>? HybridBotMessageReady;
    public event EventHandler<HybridAdaptiveCardEventArgs>? HybridAdaptiveCardReady;
    public event EventHandler<HybridCardStateChangedEventArgs>? HybridCardStateChanged;
    public event EventHandler<HybridChatEventArgs>? HybridChatEventRaised;

    public HybridChatServiceV2(
        ILogger<HybridChatServiceV2> logger,
        InsuranceAgentService agentService,
        IConversationContext context) {
        _logger = logger;
        _agentService = agentService;
        _context = context;

        SubscribeToAgentEvents();

        _logger.LogInformation("✅ HybridChatServiceV2 initialized (no SemanticKernel, no domain init)");
    }

    // ------------------------------------------------------
    // 🧩 Agent → UI event forwarding
    // ------------------------------------------------------
    private void SubscribeToAgentEvents() {
        // ─── Standard chat messages ─────────────────────────────
        _agentService.ActivityMessageReady += (s, e) => {
            _logger.LogInformation("[HybridChatServiceV2] Forwarding ActivityMessageReady: {Content}",
                e.Message.Content);
            OnHybridBotMessageReady(e.Message);
        };

        // ─── Adaptive Cards ─────────────────────────────────────
        _agentService.ActivityAdaptiveCardReady += (s, e) =>
            OnHybridAdaptiveCardReady(e.CardJson, e.CardId, e.RenderMode);

        // ─── Lifecycle Events ───────────────────────────────────
        _agentService.ActivityCompleted += (s, e) =>
            OnHybridChatEventRaised(new ChatEvent { Type = "ActivityCompleted", Payload = e.Context });

        _agentService.ActivityLifecycleChanged += (s, e) =>
            OnHybridChatEventRaised(new ChatEvent { Type = "ActivityLifecycleChanged", Payload = e });

        _agentService.TopicLifecycleChanged += (s, e) =>
            OnHybridChatEventRaised(new ChatEvent { Type = "TopicLifecycleChanged", Payload = e });

        _agentService.TopicInserted += (s, e) =>
            OnHybridChatEventRaised(new ChatEvent { Type = "TopicInserted", Payload = e });

        // ─── Reset & Prompt ─────────────────────────────────────
        _agentService.ConversationReset += (s, e) => {
            _logger.LogInformation("[HybridChatServiceV2] ConversationReset → UI");
            OnConversationReset?.Invoke(this, e);
        };

        _agentService.PromptInputStateChanged += (s, e) => {
            _logger.LogInformation("[HybridChatServiceV2] PromptInputStateChanged → Enabled={Enabled}, CardId={CardId}",
                e.IsEnabled, e.CardId);
            PromptInputStateChanged?.Invoke(this, e);
        };

        // ─── Card States ────────────────────────────────────────
        _agentService.CardStateChanged += (s, e) => {
            _logger.LogInformation("[HybridChatServiceV2] CardStateChanged: {CardId} -> {State}",
                e.CardId, e.State);
            HybridCardStateChanged?.Invoke(this, new HybridCardStateChangedEventArgs(e.CardId, e.State));
        };
    }

    // ------------------------------------------------------
    // 🧭 Conversation lifecycle delegation
    // ------------------------------------------------------
    public void StartConversation(ChatSessionState session) {
        _logger.LogInformation("[HybridChatServiceV2] Starting conversation (delegated to Agent)");
        _ = _agentService.StartConversationAsync(session);
    }

    public async Task ResetConversationAsync(CancellationToken ct = default) {
        _logger.LogInformation("[HybridChatServiceV2] ResetConversationAsync called");
        await _agentService.ResetConversationAsync(ct);
    }

    // ------------------------------------------------------
    // ✉️ UI → Agent message relays (pass-through only)
    // ------------------------------------------------------
    public Task HandleUserMessageAsync(MessageEventArgs e) {
        _logger.LogInformation("[HybridChatServiceV2] UserMessageEntered: {Content}", e.Content);
        return _agentService.ProcessUserMessageAsync(e.Content, CancellationToken.None);
    }

    public Task HandleCardSubmitAsync(ChatCardSubmitEventArgs e) {
        _logger.LogInformation("[HybridChatServiceV2] CardSubmitted: {Keys}",
            string.Join(",", e.Data.Keys));
        return _agentService.HandleCardSubmitAsync(e.Data, CancellationToken.None);
    }

    // ------------------------------------------------------
    // 🔔 Hybrid event triggers
    // ------------------------------------------------------
    protected virtual void OnHybridBotMessageReady(ChatMessage message)
        => HybridBotMessageReady?.Invoke(this, new HybridBotMessageEventArgs(message));

    protected virtual void OnHybridAdaptiveCardReady(string cardJson, string cardId, RenderMode mode)
        => HybridAdaptiveCardReady?.Invoke(this, new HybridAdaptiveCardEventArgs(cardJson, cardId, mode));

    protected virtual void OnHybridChatEventRaised(ChatEvent chatEvent)
        => HybridChatEventRaised?.Invoke(this, new HybridChatEventArgs(chatEvent));
}
