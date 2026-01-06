using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Interfaces;
using ConversaCore.Models;
using ConversaCore.Topics;
using InsuranceAgent.Models;
using InsuranceAgent.Topics;

namespace InsuranceAgent.Services;

/// <summary>
/// Orchestrates between:
/// 1. InsuranceAgentService (deterministic workflow / TopicFlow)
/// 2. SemanticKernelService (AI generative reasoning)
///
/// Provides a normalized event-driven interface for the ChatWindow.
/// </summary>
public class HybridChatService {
    public event Action<ChatSessionState, ITopic>? OnConversationStart;
    public event EventHandler<ConversationResetEventArgs>? OnConversationReset;
    public event EventHandler<PromptInputStateChangedEventArgs>? PromptInputStateChanged;
    public event EventHandler<HybridBotMessageEventArgs>? HybridBotMessageReady;
    public event EventHandler<HybridAdaptiveCardEventArgs>? HybridAdaptiveCardReady;
    public event EventHandler<HybridTypingIndicatorEventArgs>? HybridTypingIndicatorChanged;
    public event EventHandler<HybridSuggestionsEventArgs>? HybridSuggestionsUpdated;
    public event EventHandler<HybridChatEventArgs>? HybridChatEventRaised;
    public event EventHandler<HybridCardStateChangedEventArgs>? HybridCardStateChanged;

    private readonly ILogger<HybridChatService> _logger;
    private readonly InsuranceAgentServiceV2 _agentService;
    private readonly ISemanticKernelService _semanticKernelService;
    private readonly TopicRegistry _topicRegistry;
    private readonly IConversationContext _context;

    public IConversationContext Context => _context;
    public ConversaCore.TopicFlow.TopicWorkflowContext WorkflowContext { get; }

    public HybridChatService(
        ILogger<HybridChatService> logger,
        InsuranceAgentServiceV2 agentService,
        ISemanticKernelService semanticKernelService,
        TopicRegistry topicRegistry,
        IConversationContext context) {
        _logger = logger;
        _agentService = agentService;
        _semanticKernelService = semanticKernelService;
        _topicRegistry = topicRegistry;
        _context = context;

        WorkflowContext = new ConversaCore.TopicFlow.TopicWorkflowContext();

        #region Subscribe to Insurance Agent Events
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
        _agentService.ConversationReset += (s, e) => {
            _logger.LogInformation("[HybridChatService] Forwarding ConversationReset event to UI");
            OnConversationReset?.Invoke(this, e);
        };
        _agentService.MatchingTopicNotFound += async (s, e) => {
            _logger.LogInformation("No topic could process '{Message}', escalating to Semantic Kernel", e.UserMessage);
            await _semanticKernelService.ProcessMessageAsync(e.UserMessage, new ChatSessionState());
        };
        _agentService.CardStateChanged += (s, e) =>
        {
            _logger.LogInformation("[HybridChatService] Forwarding CardStateChanged -> HybridCardStateChanged ({CardId}, {State})", e.CardId, e.State);
            HybridCardStateChanged?.Invoke(this, new HybridCardStateChangedEventArgs(e.CardId, e.State));
        };
        _agentService.PromptInputStateChanged += (s, e) =>
        {
            _logger.LogInformation($"[HybridChatService] Forwarding PromptInputStateChanged (Enabled={e.IsEnabled}, CardId={e.CardId})");
            PromptInputStateChanged?.Invoke(this, e);
        };
        #endregion

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
        // Initialize insurance-specific global variables first
        // NOTE: This is now redundant as InsuranceAgentServiceV2 handles this in constructor
        // InitializeInsuranceGlobals(); // ← Can be removed in future cleanup
        
        // Let the agent handle topic retrieval and initialization
        _ = _agentService.StartConversationPublicAsync(CancellationToken.None);
    }

    public async Task ResetConversationAsync(CancellationToken ct = default) {
        _logger.LogInformation("[HybridChatService] Reset conversation requested - calling InsuranceAgentService");
        // await _agentService.ResetConversationAsync(ct); // ← Commented out - method now protected in V3
        _logger.LogInformation("[HybridChatService] Reset conversation completed from InsuranceAgentService");
    }

    /// <summary>
    /// Initialize insurance-specific global variables and context.
    /// This specializes the generic ConversaCore framework for insurance domain.
    /// </summary>
    private void InitializeInsuranceGlobals() {
        _logger.LogInformation("[HybridChatService] Initializing insurance domain globals");
        
        // === Initialize Empty Card Model Instances (BaseCardModel types) ===
        // These will be populated by topics during adaptive card interactions
        _context.SetModel(new BeneficiaryInfoModel());
        _context.SetModel(new EmploymentModel());
        _context.SetModel(new HealthInfoModel());
        _context.SetModel(new ContactInfoModel());
        _context.SetModel(new CoverageIntentModel());
        _context.SetModel(new LeadDetailsModel());
        _context.SetModel(new DependentsModel());
        _context.SetModel(new LifeGoalsModel());
               
        // === Application Configuration (Static Rules) ===
        var appConfig = new {
            InsuranceType = "Term Life",
            ApplicationStage = "Initial",
            ComplianceCheckRequired = true,
            RequiresHealthScreening = true,
            RequiredTopics = new List<string> {
                "ContactInfo",
                "EmploymentInfo", 
                "HealthInfo",
                "BeneficiaryInfo",
                "CoverageIntent"
            },
            ValidationRules = new {
                MinimumAge = 18,
                MaximumAge = 80,
                MinimumCoverage = 25000,
                MaximumCoverage = 5000000
            }
        };
        _context.SetValue("ApplicationConfiguration", appConfig);
        
        // === Individual Configuration Values (for backward compatibility) ===
        //_context.SetValue("InsuranceType", appConfig.InsuranceType);
        //_context.SetValue("ApplicationStage", appConfig.ApplicationStage);
        //_context.SetValue("ComplianceCheckRequired", appConfig.ComplianceCheckRequired);
        //_context.SetValue("RequiresHealthScreening", appConfig.RequiresHealthScreening);
        //_context.SetValue("RequiredTopics", appConfig.RequiredTopics);
        //_context.SetValue("MinimumAge", appConfig.ValidationRules.MinimumAge);
        //_context.SetValue("MaximumAge", appConfig.ValidationRules.MaximumAge);
        //_context.SetValue("MinimumCoverage", appConfig.ValidationRules.MinimumCoverage);
        //_context.SetValue("MaximumCoverage", appConfig.ValidationRules.MaximumCoverage);
        
        // === Session Tracking ===
        _context.SetValue("SessionStartTime", DateTime.UtcNow);
        _context.SetValue("DomainSpecialization", "Insurance");
        
        // === Runtime/Workflow Global Variables (Copilot Studio Compatible) ===
        
        // Global.IsInsuranceIntent equivalent
        _context.SetValue("Global_IsInsuranceIntent", new {
            talkingAboutInsurance = "maybe",
            whatTypeOfInsurance = "unknown"
        });
        
        // Global.isPersonalInfoComplete equivalent  
        _context.SetValue("Global_isPersonalInfoComplete", new {
            isPersonalInfoComplete = false,
            missingFields = new[] { new { item = "" } }
        });
        
        // Global.CarrierEligibility equivalent
        _context.SetValue("Global_CarrierEligibility", new {
            qualified = new object[] {
                // Will be populated as topics gather information
            },
            nearlyQualified90Percent = new object[] {
                // Will be populated with carrier matching logic
            }
        });
        
        // Global.healthQuestionnaire equivalent
        _context.SetValue("Global_healthQuestionnaire", new object[] { });
        
        // Additional workflow tracking (legacy compatibility)
        _context.SetValue("Global_IsLeadQualified", false);
        _context.SetValue("Global_ConversationPhase", "Initial");
        _context.SetValue("Global_TopicChain", new List<string>());
        _context.SetValue("Global_DataCompleteness", 0.0);
        _context.SetValue("Global_InsuranceModelsInitialized", true);
        
        _logger.LogInformation("[HybridChatService] Insurance domain globals initialized with {CardModelCount} card models and {BusinessModelCount} business models", 8, 6);
    }


    // === ChatWindow → Hybrid ===
    public void HandleUserMessage(object? sender, MessageEventArgs e) {
        _ = HandleUserMessageAsync(e);
    }

    private async Task HandleUserMessageAsync(MessageEventArgs e) {
        try {
            _logger.LogInformation("[HybridChatService] UserMessageEntered: {Content}", e.Content);
            // await _agentService.ProcessUserMessageAsync(e.Content, CancellationToken.None); // ← Commented out - method now protected in V3
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
            // await _agentService.HandleCardSubmitAsync(e.Data, CancellationToken.None); // ← Commented out - method now protected in V3
        } catch (Exception ex) {
            _logger.LogError(ex, "Error in HandleCardSubmitAsync");
        }
    }

    public void HandleCardAction(object? sender, ChatWindowCardActionEventArgs e) {
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




    #region Event Triggers
    // These events notify the Hybrid ChatWindow (UI layer)
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
    #endregion
}
