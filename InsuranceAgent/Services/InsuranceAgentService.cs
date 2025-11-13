using ConversaCore.Cards;
using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.StateMachine;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.TopicFlow.Core.Interfaces;
using ConversaCore.Topics;
using InsuranceAgent.Cards;
using InsuranceAgent.Models;
using InsuranceAgent.Topics;
using System.Reflection;
using System.Text.Json;

public class InsuranceAgentService {
    private readonly TopicRegistry _topicRegistry;
    private readonly IConversationContext _context;
    private readonly TopicWorkflowContext _wfContext;
    private readonly ILogger<InsuranceAgentService> _logger;

    // Activity IDs for the compliance flowchart
    private const string ActivityId_CollectCompliance = "CollectCompliance";
    private const string ActivityId_TcpaCheck = "TcpaConsentCheck";
    private const string ActivityId_CcpaCheck = "CcpaAckCheck";
    private const string ActivityId_CaliforniaCheck = "CaliforniaCheck";
    private const string ActivityId_CollectCaliforniaInfo = "CollectCaliforniaInfo";
    private const string ActivityId_ShowHighIntentCard = "ShowHighIntentCard";
    private const string ActivityId_ShowMediumIntentCard = "ShowMediumIntentCard";
    private const string ActivityId_ShowLowIntentCard = "ShowLowIntentCard";
    private const string ActivityId_ShowBlockedCard = "ShowBlockedCard";
    private const string ActivityId_RouteToNextTopic = "RouteToNextTopic";
    private const string ActivityId_ProcessComplianceData = "ProcessComplianceData";

    private ITopic? _activeTopic;
    private string? _activeCardId;
    private const string ActiveCardKey = "ActiveCardId";

    private readonly Stack<TopicFlow> _pausedTopics = new Stack<TopicFlow>(); // Track paused topics

    // NEW: Event-driven sub-topic completion tracking
    private readonly Dictionary<string, PendingSubTopic> _pendingSubTopics = new Dictionary<string, PendingSubTopic>();

    private class PendingSubTopic {
        public TopicFlow CallingTopic { get; set; } = null!;
        public TopicFlow SubTopic { get; set; } = null!;
        public string CallingTopicName { get; set; } = string.Empty;
        public string SubTopicName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
    }

    // Outbound events → HybridChatService
    public event EventHandler<ActivityMessageEventArgs>? ActivityMessageReady;
    public event EventHandler<ActivityAdaptiveCardEventArgs>? ActivityAdaptiveCardReady;
    public event EventHandler<ActivityCompletedEventArgs>? ActivityCompleted;
    public event EventHandler<TopicLifecycleEventArgs>? TopicLifecycleChanged;
    public event EventHandler<MatchingTopicNotFoundEventArgs>? MatchingTopicNotFound;
    public event EventHandler<TopicInsertedEventArgs>? TopicInserted;
    public event EventHandler<ActivityLifecycleEventArgs>? ActivityLifecycleChanged;
    public event EventHandler<ConversationResetEventArgs>? ConversationReset;
    public event EventHandler<PromptInputStateChangedEventArgs>? PromptInputStateChanged;
    public event EventHandler<CustomEventTriggeredEventArgs>? CustomEventTriggered;

    public InsuranceAgentService(
        TopicRegistry topicRegistry,
        IConversationContext context,
        TopicWorkflowContext wfContext,
        ILogger<InsuranceAgentService> logger) {
        _topicRegistry = topicRegistry;
        _context = context;
        _wfContext = wfContext;
        _logger = logger;
    }

    public async Task ProcessUserMessageAsync(string userMessage, CancellationToken ct = default) {
        var (topic, confidence) = await _topicRegistry.FindBestTopicAsync(userMessage, _context, ct);

        #region 🔧 Fallback topic setup
        // Resolve fallback once for all branches
        var fallbackTopic = _topicRegistry.GetTopic("FallbackTopic") as TopicFlow;

        // If neither a topic match nor a fallback exist → cannot continue
        if (topic == null) {
            if (fallbackTopic == null) {
                _logger.LogWarning("[InsuranceAgentService] FallbackTopic not found in registry.");
                MatchingTopicNotFound?.Invoke(this, new MatchingTopicNotFoundEventArgs(userMessage));
                return;
            }

            // ✅ Fallback topic is available → inject user message
            fallbackTopic.Context.SetValue("Fallback_UserPrompt", userMessage);
            _logger.LogInformation("[InsuranceAgentService] Injected Fallback_UserPrompt into FallbackTopic.");

            // Determine if the current flow is paused
            if (_activeTopic is TopicFlow activeFlow &&
                activeFlow.GetCurrentActivity() is IPausableActivity waitingActivity &&
                waitingActivity.IsPaused) {
                var currentAct = waitingActivity as TopicFlowActivity;
                _logger.LogInformation("[InsuranceAgentService] Detected paused activity {ActivityId}. Routing message to FallbackTopic.",
                    currentAct?.Id ?? "(unknown)");

                // Temporarily suspend current flow
                _pausedTopics.Push(activeFlow);
                UnhookTopicEvents(activeFlow);
            }

            // Always switch active topic to fallback
            _activeTopic = fallbackTopic;
            HookTopicEvents(fallbackTopic);

            await fallbackTopic.RunAsync(ct);
            return;
        }
        #endregion

        #region 🚀 Matched topic logic
        if (topic is not TopicFlow flow) {
            _logger.LogWarning("Resolved topic is not a TopicFlow: {Type}", topic.GetType().Name);
            return;
        }

        if (_activeTopic is TopicFlow currentActiveTopic)
            UnhookTopicEvents(currentActiveTopic);

        _activeTopic = flow;
        HookTopicEvents(flow);

        _logger.LogInformation("Activated topic {TopicName} (confidence {Confidence:P2})", flow.Name, confidence);
        await flow.RunAsync(ct);
        #endregion
    }


    public async Task HandleCardSubmitAsync(Dictionary<string, object> data, CancellationToken ct) {
        if (_activeTopic is TopicFlow flow) {
            var current = flow.GetCurrentActivity();

            if (current is IAdaptiveCardActivity cardAct) {
                _logger.LogInformation("[InsuranceAgentService] Delivering card input to {ActivityId}", current.Id);
                cardAct.OnInputCollected(new AdaptiveCardInputCollectedEventArgs(data));

                // Instead of ResumeAsync, just advance the queue
                await flow.StepAsync(null, ct);
            }
            else {
                _logger.LogWarning("[InsuranceAgentService] Current activity {ActivityId} is not adaptive-card-capable", current?.Id);
                RaiseMessage("⚠️ This step cannot accept card input.");
            }
        }
        else {
            RaiseMessage("⚠️ Unable to resume workflow (no active topic).");
        }
    }

    /// <summary>
    /// Handles completion of the FallbackTopic, resuming any paused activity.
    /// </summary>
    private async Task HandleFallbackCompletion(TopicFlow fallbackTopic, TopicResult result) {
        if (_pausedTopics.Count == 0) {
            _logger.LogInformation("[InsuranceAgentService] FallbackTopic completed but no paused topics to resume.");
            return;
        }

        var callingTopic = _pausedTopics.Pop();
        _logger.LogInformation("[InsuranceAgentService] Resuming paused topic '{Topic}' after fallback answer.", callingTopic.Name);

        // Unhook fallback and rehook the main topic
        UnhookTopicEvents(fallbackTopic);
        _activeTopic = callingTopic;
        HookTopicEvents(callingTopic);

        // ✅ NEW: Reactivate the previously active card before resuming
        var previousCard = _context.GetValue<string>(ActiveCardKey);
        if (!string.IsNullOrEmpty(previousCard)) {
            _logger.LogInformation("[InsuranceAgentService] Reactivating card {CardId} after fallback completion", previousCard);
            OnCardStateChanged(previousCard, CardState.Active);
        }

        try {
            // Resume last paused activity (e.g., AdaptiveCard)
            var current = callingTopic.GetCurrentActivity() as IPausableActivity;
            if (current != null) {
                await current.ResumeAsync("FallbackTopic completed", CancellationToken.None);
            }
            else {
                _logger.LogInformation("[InsuranceAgentService] No pausable activity found, resuming topic normally.");
                await callingTopic.ResumeAsync("FallbackTopic completed", CancellationToken.None);
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "[InsuranceAgentService] Error resuming topic '{TopicName}' after fallback.", callingTopic.Name);
        }
    }


    // =====================================
    //  HOOK EVENTS
    // =====================================
    private void HookTopicEvents(TopicFlow flow) {
        // --- Topic-level ---
        flow.TopicLifecycleChanged += HandleTopicLifecycleChanged;
        flow.TopicLifecycleChanged += OnTopicLifecycleChanged; // completion tracking
        flow.ActivityCreated += OnActivityCreated;
        flow.ActivityCompleted += HandleActivityCompleted;
        flow.TopicInserted += HandleTopicInserted;

        // --- Topic-level triggers ---
        if (flow is ITopicTriggeredActivity topicTrigger) {
            Console.WriteLine($"[InsuranceAgentService.HookTopicEvents] Subscribing to TopicTriggered for topic '{flow.Name}'");
            topicTrigger.TopicTriggered += OnTopicTriggered;
        }

        // --- Activity-level ---
        foreach (var act in flow.GetAllActivities()) {
            act.ActivityLifecycleChanged += HandleActivityLifecycleChanged;
            act.MessageEmitted += HandleMessageEmitted;

            // Adaptive cards
            if (act is IAdaptiveCardActivity cardAct) {
                cardAct.CardJsonSent += HandleCardJsonSent;
                cardAct.ValidationFailed += HandleValidationFailed;
                cardAct.CardJsonEmitted += HandleCardJsonEmitted;
                cardAct.CardJsonSending += HandleCardJsonSending;
                cardAct.CardJsonRendered += HandleCardJsonRendered;
                cardAct.CardDataReceived += HandleCardDataReceived;
                cardAct.ModelBound += HandleModelBound;
            }

            // Topic-triggered
            if (act is ITopicTriggeredActivity trigger)
                trigger.TopicTriggered += OnTopicTriggered;

            // Custom event-triggered
            if (act is ICustomEventTriggeredActivity customTrigger)
                customTrigger.CustomEventTriggered += OnCustomEventTriggered;

            // Conditional containers
            if (act is ConditionalActivity<TriggerTopicActivity> conditional) {
                conditional.TopicTriggered += OnTopicTriggered;
                conditional.CustomEventTriggered += OnCustomEventTriggered;
            }

            // Composite containers
            if (act is CompositeActivity composite) {
                composite.TopicTriggered += OnTopicTriggered;
                composite.CustomEventTriggered += OnCustomEventTriggered;
            }
        }
    }


    // =====================================
    //  UNHOOK EVENTS
    // =====================================
    private void UnhookTopicEvents(TopicFlow flow) {
        if (flow == null)
            return;

        _logger.LogInformation("[InsuranceAgentService] Unhooking events for topic flow: {FlowName}", flow.Name);

        // --- 🔹 Deactivate any card still marked active ---
        var activeCard = _context.GetValue<string>(ActiveCardKey);
        if (!string.IsNullOrEmpty(activeCard)) {
            _logger.LogInformation("[InsuranceAgentService] Deactivating lingering active card {CardId} before unhook", activeCard);
            OnCardStateChanged(activeCard, CardState.ReadOnly);
            _wfContext.RemoveValue(ActiveCardKey);
        }

        // --- existing unhook logic follows ---
        flow.TopicLifecycleChanged -= HandleTopicLifecycleChanged;
        flow.TopicLifecycleChanged -= OnTopicLifecycleChanged;
        flow.ActivityCreated -= OnActivityCreated;
        flow.ActivityCompleted -= HandleActivityCompleted;
        flow.TopicInserted -= HandleTopicInserted;

        if (flow is ITopicTriggeredActivity topicTrigger)
            topicTrigger.TopicTriggered -= OnTopicTriggered;

        foreach (var act in flow.GetAllActivities()) {
            if (act is IAdaptiveCardActivity cardAct) {
                cardAct.CardJsonSent -= HandleCardJsonSent;
                cardAct.ValidationFailed -= HandleValidationFailed;
                cardAct.CardJsonEmitted -= HandleCardJsonEmitted;
                cardAct.CardJsonSending -= HandleCardJsonSending;
                cardAct.CardJsonRendered -= HandleCardJsonRendered;
                cardAct.CardDataReceived -= HandleCardDataReceived;
                cardAct.ModelBound -= HandleModelBound;
            }

            if (act is ITopicTriggeredActivity trigger)
                trigger.TopicTriggered -= OnTopicTriggered;

            if (act is ICustomEventTriggeredActivity customTrigger)
                customTrigger.CustomEventTriggered -= OnCustomEventTriggered;

            if (act is ConditionalActivity<TriggerTopicActivity> conditional) {
                conditional.TopicTriggered -= OnTopicTriggered;
                conditional.CustomEventTriggered -= OnCustomEventTriggered;
            }

            if (act is CompositeActivity composite) {
                composite.TopicTriggered -= OnTopicTriggered;
                composite.CustomEventTriggered -= OnCustomEventTriggered;
            }
        }

        // Reset state trackers
        _activeCardId = null;

        _logger.LogInformation("[InsuranceAgentService] ✅ Unhooked topic and activity events for {FlowName}", flow.Name);
    }



    // =====================================
    //  HANDLERS
    // =====================================
    private async void HandleTopicLifecycleChanged(object? sender, TopicLifecycleEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] TopicLifecycleChanged: {Topic} -> {State}", e.TopicName, e.State);

        // ───────────────────────────────────────────────
        // 🧭 Always propagate lifecycle changes upward
        // ───────────────────────────────────────────────
        TopicLifecycleChanged?.Invoke(this, e);

        // ───────────────────────────────────────────────
        // 🧩 Handle sub-topic completion → resume parent
        // ───────────────────────────────────────────────
        if (e.State == TopicLifecycleState.Completed) {
            if (_pausedTopics.Count == 0) {
                _logger.LogDebug("[InsuranceAgentService] Topic '{Topic}' completed, but no paused topics remain.", e.TopicName);
                return;
            }

            try {
                // 🪜 Pop the parent topic (LIFO order)
                var parentTopic = _pausedTopics.Pop();
                var completedTopic = sender ?? _activeTopic;

                _logger.LogInformation(
                    "[InsuranceAgentService] 🧩 Resuming parent topic '{Parent}' after sub-topic '{Completed}' completed.",
                    parentTopic.Name, e.TopicName);

                // 🧭 Update the active topic reference immediately
                var previousTopic = _activeTopic;
                _activeTopic = parentTopic;

                _logger.LogInformation(
                    "[InsuranceAgentService] 🔄 Active topic updated: {Old} → {New}",
                    previousTopic?.Name ?? "(null)", parentTopic.Name);

                // ♻️ Re-hook lifecycle + activity events to restore flow monitoring
                HookTopicEvents(parentTopic);

                // ✅ Restore parent's saved card state if applicable
                var savedCardId = _context.GetValue<string>($"{parentTopic.Name}_SavedCardId");
                if (!string.IsNullOrEmpty(savedCardId)) {
                    _context.SetValue(ActiveCardKey, savedCardId);
                    _logger.LogInformation(
                        "[InsuranceAgentService] Restored parent card '{CardId}' after sub-topic '{Completed}' completion.",
                        savedCardId, e.TopicName);

                    OnCardStateChanged(savedCardId, CardState.Active);
                }
                else {
                    _logger.LogInformation(
                        "[InsuranceAgentService] No saved card state found for parent topic '{Parent}'.",
                        parentTopic.Name);
                }

                // 🧠 Ensure the parent’s internal flow is in sync
                if (parentTopic is TopicFlow parentFlow) {
                    _logger.LogDebug(
                        "[InsuranceAgentService] Parent flow context restored → CurrentActivity='{Activity}' | State='{State}'",
                        parentFlow.GetCurrentActivity()?.Id ?? "(none)",
                        parentFlow.State);
                }
                else {
                    _logger.LogWarning(
                        "[InsuranceAgentService] Parent topic '{Parent}' has no TopicFlow instance.",
                        parentTopic.Name);
                }

                // 🚀 Resume the parent topic’s execution loop
                try {
                    _logger.LogInformation(
                        "[InsuranceAgentService] ▶ Resuming execution of parent topic '{Parent}'...",
                        parentTopic.Name);

                    await parentTopic.RunAsync();

                    _logger.LogInformation(
                        "[InsuranceAgentService] ✅ Parent topic '{Parent}' successfully resumed execution.",
                        parentTopic.Name);
                } catch (Exception resumeEx) {
                    _logger.LogError(
                        resumeEx,
                        "[InsuranceAgentService] ❌ Exception while resuming parent topic '{Parent}'.",
                        parentTopic.Name);
                }

                _logger.LogInformation(
                    "[InsuranceAgentService] 🌿 Resume sequence completed for parent topic '{Parent}'.",
                    parentTopic.Name);
            } catch (Exception ex) {
                _logger.LogError(
                    ex,
                    "[InsuranceAgentService] ❌ Unexpected error while resuming paused topic after sub-topic completion.");
            }
        }
    }


    private void HandleActivityCompleted(object? sender, ActivityCompletedEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] ActivityCompleted -> {ActivityId}", e.ActivityId);
        ActivityCompleted?.Invoke(this, e);
    }

    private void HandleTopicInserted(object? sender, TopicInsertedEventArgs e)
        => TopicInserted?.Invoke(this, e);

    private void HandleActivityLifecycleChanged(object? sender, ActivityLifecycleEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] ActivityLifecycleChanged: {ActivityId} -> {State} | Data={Data}",
            e.ActivityId, e.State, e.Data);
        ActivityLifecycleChanged?.Invoke(this, e);
    }

    private void HandleMessageEmitted(object? sender, MessageEmittedEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] MessageEmitted from {ActivityId}: {Message}", e.ActivityId, e.Message);
        ActivityMessageReady?.Invoke(this, new ActivityMessageEventArgs(
            new ChatMessage {
                Content = e.Message,
                IsFromUser = false,
                Timestamp = DateTime.Now
            }));
    }


    // --- Adaptive Card handlers ---
    private void HandleCardJsonSent(object? sender, CardJsonEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] CardJsonSent {CardId} (IsRequired={IsRequired})", e.CardId, e.IsRequired);

        // ───────────────────────────────────────────────
        // 🧩 Preserve parent card before overwrite
        // ───────────────────────────────────────────────
        try {
            // If there is a paused topic (meaning we just entered a sub-topic),
            // we save the currently active card ID under that parent’s context key
            if (_pausedTopics.Count > 0) {
                var parentTopic = _pausedTopics.Peek();
                var currentActiveCard = _context.GetValue<string>(ActiveCardKey);

                if (!string.IsNullOrEmpty(currentActiveCard) && currentActiveCard != e.CardId) {
                    _context.SetValue($"{parentTopic.Name}_SavedCardId", currentActiveCard);
                    _logger.LogInformation(
                        "[InsuranceAgentService] Preserved parent card {CardId} under paused topic '{TopicName}'",
                        currentActiveCard, parentTopic.Name);
                }
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "[InsuranceAgentService] Error while preserving parent card before new card activation");
        }

        // ───────────────────────────────────────────────
        // 🔻 Deactivate previous card
        // ───────────────────────────────────────────────
        var previousCard = _context.GetValue<string>(ActiveCardKey);
        if (!string.IsNullOrEmpty(previousCard) && previousCard != e.CardId) {
            _logger.LogInformation("[InsuranceAgentService] Deactivating previous card {CardId}", previousCard);
            OnCardStateChanged(previousCard, CardState.ReadOnly);
        }

        // ───────────────────────────────────────────────
        // 🔺 Activate this new card
        // ───────────────────────────────────────────────
        _context.SetValue(ActiveCardKey, e.CardId);
        _context.SetValue($"{e.CardId}_IsRequired", e.IsRequired); // Track required flag

        OnCardStateChanged(e.CardId, CardState.Active);

        // ───────────────────────────────────────────────
        // 📤 Forward to UI (include IsRequired)
        // ───────────────────────────────────────────────

        _logger.LogInformation($"[InsuranceAgentService] right before triggering ActivityAdaptiveCardReady for {e.CardId}");
        ActivityAdaptiveCardReady?.Invoke(this,
            new ActivityAdaptiveCardEventArgs(
                e.CardJson ?? "{}",
                e.CardId,
                e.RenderMode,
                e.IsRequired));

        // ───────────────────────────────────────────────
        // 🧱 Handle prompt enable/disable for required cards
        // ───────────────────────────────────────────────
        try {
            if (e.IsRequired) {
                _logger.LogInformation("[InsuranceAgentService] Disabling user prompt — card {CardId} is required.", e.CardId);
                PromptInputStateChanged?.Invoke(this, new PromptInputStateChangedEventArgs(false, e.CardId));
            }
            else {
                _logger.LogInformation("[InsuranceAgentService] Enabling user prompt — card {CardId} is not required.", e.CardId);
                PromptInputStateChanged?.Invoke(this, new PromptInputStateChangedEventArgs(true, e.CardId));
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "[InsuranceAgentService] Error toggling prompt state for card {CardId}", e.CardId);
        }
    }

    private void HandleValidationFailed(object? sender, ValidationFailedEventArgs e) {
        _logger.LogWarning("[InsuranceAgentService] ValidationFailed: {Error}", e.Exception?.Message);

        if (!string.IsNullOrEmpty(_activeCardId))
            OnCardStateChanged(_activeCardId, CardState.Active);
    }

    // Internal tracing hooks
    private void HandleCardJsonEmitted(object? s, EventArgs e) => _logger.LogDebug("CardJsonEmitted (internal)");
    private void HandleCardJsonSending(object? s, EventArgs e) => _logger.LogDebug("CardJsonSending (internal)");
    private void HandleCardJsonRendered(object? s, EventArgs e) => _logger.LogDebug("CardJsonRendered (client ack)");
    private void HandleCardDataReceived(object? s, EventArgs e) => _logger.LogDebug("CardDataReceived (internal)");
    private void HandleModelBound(object? s, EventArgs e) => _logger.LogDebug("ModelBound (internal)");

    private void OnActivityCreated(object? sender, ActivityCreatedEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] ActivityCreated -> {PayloadType}: {Content}",
            e.Content?.GetType().Name, e.Content);

        switch (e.Content) {
            default:
                _logger.LogDebug("Unhandled activity payload type {Type}", e.Content?.GetType().Name);
                break;
        }
    }

    private void RaiseMessage(string content) {
        ActivityMessageReady?.Invoke(this,
            new ActivityMessageEventArgs(new ChatMessage {
                Content = content,
                IsFromUser = false,
                Timestamp = DateTime.Now
            }));
    }

    /// <summary>
    /// Adds domain-specific activities to the ConversationStartTopic.
    /// This ensures proper separation of concerns between system and domain activities.
    /// </summary>
    /// <param name="flow">The ConversationStartTopic flow instance</param>
    private void AddDomainActivitiesToStartTopic(TopicFlow flow) {
        _logger.LogInformation("[InsuranceAgentService] Adding domain-specific activities to ConversationStartTopic");

        // 👋 Add greeting first
        flow.Add(new GreetingActivity("Greet"));

        // Add compliance trigger
        flow.Add(new TriggerTopicActivity(ActivityId_CollectCompliance, "ComplianceTopic", _logger, waitForCompletion: true));

        // Add a simple activity to process the compliance data before continuing
        flow.Add(new SimpleActivity(ActivityId_ProcessComplianceData, (ctx, input) => {
            _logger.LogInformation("[InsuranceAgentService] Processing compliance data before decision tree");
            // No additional processing needed, just act as a buffer between topic completion and decision tree
            return Task.FromResult<object?>(null);
        }));

        // Add the complex compliance flowchart
        flow.AddRange(ComplianceFlowActivities() ?? new List<TopicFlowActivity>());

        _logger.LogInformation("[InsuranceAgentService] Domain-specific activities added to ConversationStartTopic");
    }


    public async Task StartConversationAsync(ChatSessionState sessionState, CancellationToken ct = default) {
        // Clear any previous execution state
        _pausedTopics.Clear();

        var topic = _topicRegistry.GetTopic("ConversationStart");

        if (topic == null) {
            _logger.LogWarning("ConversationStartTopic not found in registry.");
            return;
        }

        if (topic is TopicFlow flow) {
            // Make sure we're not duplicating activities
            // Remove existing activities with the same IDs to be safe
            flow.RemoveActivity(ActivityId_CollectCompliance);
            flow.RemoveActivity("ProcessComplianceData");
            flow.RemoveActivity("TCPAConsentSwitch");

            // Now use the extracted method to add domain-specific activities
            AddDomainActivitiesToStartTopic(flow);

            // Unhook events from previous topic before switching
            if (_activeTopic is TopicFlow currentActiveTopic) {
                UnhookTopicEvents(currentActiveTopic);
            }

            _activeTopic = flow;
            HookTopicEvents(flow);

            _logger.LogInformation("ConversationStartTopic activated");
            await flow.RunAsync(ct);
        }
        else {
            _logger.LogWarning("ConversationStartTopic is not a TopicFlow: {Type}", topic.GetType().Name);
        }
    }

    public async Task ResetConversationAsync(CancellationToken ct = default) {
        _logger.LogInformation("[InsuranceAgentService] Resetting conversation...");

        // Clear all runtime collections
        _pausedTopics.Clear();
        _pendingSubTopics.Clear();

        // Unhook any active topic events
        if (_activeTopic is TopicFlow currentTopic) {
            UnhookTopicEvents(currentTopic);
            _logger.LogInformation("[InsuranceAgentService] Unhooked topic events from {TopicName}", currentTopic.Name);
        }

        _activeTopic = null;

        // Reset all contexts
        _context.Reset();
        _wfContext.Clear();

        _logger.LogInformation("[InsuranceAgentService] all context entries were purged");

        // --- Reset all topics to a clean state ---
        foreach (var topic in _topicRegistry.GetAllTopics()) {
            if (topic is TopicFlow tf) {
                _logger.LogInformation("[InsuranceAgentService] Resetting topic: {TopicName}", tf.Name);
                tf.Reset();
            }
        }

        // --- Reset ConversationStartTopic and compliance flags ---
        if (_topicRegistry.GetTopic("ConversationStart") is TopicFlow conversationStartTopic) {
            _logger.LogInformation("[InsuranceAgentService] Resetting ConversationStartTopic runtime flags");

            conversationStartTopic.Context.SetValue("ConversationStartTopic.HasRun", false);
            _context.SetValue("ConversationStartTopic.HasRun", false);

            _context.SetValue("Global_ConversationActive", false);
            _context.SetValue("Global_TopicHistory", new List<string>());
            _context.SetValue("Global_UserInteractionCount", 0);

            // Force the FSM to idle
            ForceTopicStateMachineToIdle(conversationStartTopic, "ConversationStartTopic");

            // --- Also reset ComplianceTopic for a clean restart ---
            if (_topicRegistry.GetTopic("ComplianceTopic") is TopicFlow complianceTopic) {
                ForceTopicStateMachineToIdle(complianceTopic, "ComplianceTopic");

                _wfContext.SetValue("ShowComplianceCard_Sent", null);
                _wfContext.SetValue("ShowComplianceCard_Rendered", null);
                _wfContext.SetValue("ShowComplianceCard_Completed", null);
                _wfContext.SetValue("ComplianceTopic_Completed", null);
                _wfContext.SetValue("ComplianceTopic_HasRun", null);

                _context.SetValue("compliance_data", new Dictionary<string, object>());
                _context.SetValue("tcpa_consent", false);
                _context.SetValue("ccpa_acknowledgment", false);

                _logger.LogInformation("[InsuranceAgentService] ComplianceTopic data and flags cleared");
            }
        }

        _logger.LogInformation("[InsuranceAgentService] ✅ All topics and contexts fully reset");

        // Fire event for the UI to clear chat content
        ConversationReset?.Invoke(this, new ConversationResetEventArgs());

        // Small delay to allow the UI to visually reset
        await Task.Delay(150, ct);

        // Restart cleanly
        _logger.LogInformation("[InsuranceAgentService] Restarting conversation after reset...");
        await StartConversationAsync(new ChatSessionState(), ct);
    }

    private void ForceTopicStateMachineToIdle(TopicFlow topic, string name) {
        var fsmField = topic.GetType().BaseType?
            .GetField("_fsm", BindingFlags.NonPublic | BindingFlags.Instance);

        if (fsmField?.GetValue(topic) is ITopicStateMachine<TopicFlow.FlowState> fsm) {
            fsm.ForceState(TopicFlow.FlowState.Idle, $"Forced reset to Idle in {name}");
            fsm.ClearTransitionHistory();
            _logger.LogInformation("[InsuranceAgentService] {Name} FSM forced to Idle", name);
        }
        else {
            _logger.LogWarning("[InsuranceAgentService] Could not access FSM for {Name}", name);
        }
    }

    /// <summary>
    /// Handles the completion of a sub-topic and resumes the calling topic.
    /// </summary>
    private async Task HandleSubTopicCompletion(TopicFlow completedSubTopic, TopicResult subTopicResult) {
        if (_pausedTopics.Count == 0) {
            _logger.LogWarning("[InsuranceAgentService] Sub-topic '{SubTopic}' completed but no paused topics to resume",
                completedSubTopic.Name);
            return;
        }

        var callingTopic = _pausedTopics.Pop();
        _logger.LogInformation("[InsuranceAgentService] Sub-topic '{SubTopic}' completed, resuming '{CallingTopic}'",
            completedSubTopic.Name, callingTopic.Name);

        // Set completion data in context for the calling topic to access
        var completionData = new Dictionary<string, object>();
        if (subTopicResult.wfContext != null) {
            foreach (var key in subTopicResult.wfContext.GetKeys()) {
                var value = subTopicResult.wfContext.GetValue<object>(key);
                if (value != null)
                    completionData[key] = value;
            }
        }

        _wfContext.SetValue("SubTopicCompletionData", completionData);

        var callInfo = _context.PopTopicCall(completionData);
        if (callInfo != null) {
            _wfContext.SetValue("ResumeData", callInfo.ResumeData);
            _logger.LogInformation("[InsuranceAgentService] Call info retrieved: {CallingTopic} -> {SubTopic}",
                callInfo.CallingTopicName, callInfo.SubTopicName);
        }

        // Unhook old topic and reattach to the one we’re resuming
        if (_activeTopic is TopicFlow currentActiveTopic) {
            UnhookTopicEvents(currentActiveTopic);
        }

        _activeTopic = callingTopic;
        HookTopicEvents(callingTopic);

        // ✅ NEW: Reactivate previous card before resuming
        var previousCard = _context.GetValue<string>(ActiveCardKey);
        if (!string.IsNullOrEmpty(previousCard)) {
            _logger.LogInformation("[InsuranceAgentService] Reactivating card {CardId} after sub-topic completion", previousCard);
            OnCardStateChanged(previousCard, CardState.Active);
        }

        try {
            // Continue execution from where it left off
            await callingTopic.ResumeAsync("Sub-topic completed", CancellationToken.None);
        } catch (Exception ex) {
            _logger.LogError(ex, "[InsuranceAgentService] Error resuming topic '{TopicName}' after sub-topic completion",
                callingTopic.Name);
        }
    }

    private async void OnTopicTriggered(object? sender, TopicTriggeredEventArgs e) {
        var senderType = sender?.GetType().Name ?? "Unknown";
        var senderId = sender is TopicFlowActivity activity
            ? activity.Id
            : (sender as TopicFlow)?.Name ?? "Unknown";

        // ───────────────────────────────────────────────
        // 🔔 Primary enriched event logging
        // ───────────────────────────────────────────────
        _logger.LogInformation(
            "[InsuranceAgentService.OnTopicTriggered] 🔔 TopicTriggered received → " +
            "Topic={Topic}, Origin={Origin}, BranchPath={Path}",
            e.TopicName,
            e.OriginActivityId ?? "Unknown",
            e.BranchPath ?? "(none)"
        );

        Console.WriteLine($"[InsuranceAgentService.OnTopicTriggered] *** EVENT RECEIVED ***");
        Console.WriteLine($"TopicTriggered event received from sender '{senderId}' ({senderType}) for topic '{e.TopicName}'");
        Console.WriteLine($"OriginActivityId: {e.OriginActivityId ?? "(null)"}, BranchPath: {e.BranchPath ?? "(none)"}");
        Console.WriteLine($"[InsuranceAgentService.OnTopicTriggered] *** SENDER DETAILS *** Sender type: {sender?.GetType().FullName}, Is TopicFlow: {sender is TopicFlow}, Is Activity: {sender is TopicFlowActivity}");

        // ───────────────────────────────────────────────
        // 🧩 Extract WaitForCompletion
        // ───────────────────────────────────────────────
        if (sender is not ITopicTriggeredActivity trigger) {
            _logger.LogWarning("[InsuranceAgentService] Topic triggered by unsupported sender type: {SenderType}", sender?.GetType().Name);
            Console.WriteLine($"[InsuranceAgentService.OnTopicTriggered] ERROR: Sender does not implement ITopicTriggeredActivity - ignoring event");
            return;
        }

        var waitForCompletion = trigger.WaitForCompletion;
        _logger.LogInformation("[InsuranceAgentService] Topic triggered: {TopicName} (WaitForCompletion: {WaitForCompletion})",
            e.TopicName, waitForCompletion);
        Console.WriteLine($"[InsuranceAgentService.OnTopicTriggered] Processing topic trigger: Topic='{e.TopicName}', WaitForCompletion={waitForCompletion}");

        var nextTopic = _topicRegistry.GetTopic(e.TopicName);
        if (nextTopic is TopicFlow nextFlow) {
            // ───────────────────────────────────────────────
            // 🧱 Guard against duplicate topic activation
            // ───────────────────────────────────────────────
            if (_activeTopic != null && _activeTopic.Name == e.TopicName) {
                _logger.LogWarning("[InsuranceAgentService] Topic '{TopicName}' is already active, ignoring duplicate trigger", e.TopicName);
                Console.WriteLine($"[InsuranceAgentService.OnTopicTriggered] DUPLICATE IGNORED: Topic '{e.TopicName}' is already active");
                return;
            }

            // ───────────────────────────────────────────────
            // 🌿 WaitForCompletion → sub-topic mode
            // 🧱 else → legacy handoff
            // ───────────────────────────────────────────────
            if (waitForCompletion) {
                if (_activeTopic is TopicFlow currentFlow) {
                    _logger.LogInformation(
                        "[InsuranceAgentService] Pausing topic '{CurrentTopic}' for sub-topic '{SubTopic}'",
                        currentFlow.Name, nextFlow.Name);

                    // ✅ Preserve parent’s active card before pausing
                    var parentCard = _context.GetValue<string>(ActiveCardKey);
                    if (!string.IsNullOrEmpty(parentCard)) {
                        _wfContext.SetValue($"{currentFlow.Name}_SavedCardId", parentCard);
                        _logger.LogInformation(
                            "[InsuranceAgentService] Saved parent card {CardId} for topic '{TopicName}'",
                            parentCard, currentFlow.Name);
                    }

                    // ✅ Push the parent to paused stack
                    _pausedTopics.Push(currentFlow);

                    // ✅ Unhook parent topic events while paused
                    UnhookTopicEvents(currentFlow);

                    // ✅ Hook events for sub-topic so lifecycle changes bubble up
                    _logger.LogInformation("[InsuranceAgentService] Hooking events for sub-topic {SubTopic}", nextFlow.Name);
                    HookTopicEvents(nextFlow);

                    // ✅ Track sub-topic for completion handling
                    if (!_pendingSubTopics.ContainsKey(nextFlow.Name)) {
                        _pendingSubTopics[nextFlow.Name] = new PendingSubTopic {
                            SubTopic = nextFlow,
                            SubTopicName = nextFlow.Name,
                            CallingTopicName = currentFlow.Name,
                            StartTime = DateTime.UtcNow
                        };
                    }

                    _logger.LogInformation(
                        "[InsuranceAgentService] Registered sub-topic '{SubTopic}' under parent '{Parent}' for completion tracking",
                        nextFlow.Name, currentFlow.Name);
                }
            }
            else {
                // 🧱 Legacy handoff (non-blocking trigger)
                _logger.LogWarning(
                    "[InsuranceAgentService] ⚠ Legacy hand-off mode: transitioning from '{CurrentTopic}' to '{NewTopic}'",
                    _activeTopic?.Name ?? "(none)", nextFlow.Name);

                if (_activeTopic is TopicFlow currentFlow) {
                    // Cleanup paused stack if any old topics remain
                    if (_pausedTopics.Count > 0) {
                        _logger.LogInformation(
                            "[InsuranceAgentService] Clearing paused topics before legacy hand-off (stack size={Count})",
                            _pausedTopics.Count);
                        _pausedTopics.Clear();
                    }

                    // Unhook old topic events for clean transition
                    UnhookTopicEvents(currentFlow);
                }

                // Directly hook and start the next flow in non-blocking mode
                HookTopicEvents(nextFlow);
            }


            // ───────────────────────────────────────────────
            // 🔄 Swap active topic & hook/unhook events
            // ───────────────────────────────────────────────
            if (_activeTopic is TopicFlow currentActiveTopic)
                UnhookTopicEvents(currentActiveTopic);

            _activeTopic = nextFlow;
            HookTopicEvents(nextFlow);

            // ───────────────────────────────────────────────
            // 🧭 Register sub-topic tracking
            // ───────────────────────────────────────────────
            if (waitForCompletion) {
                var callingTopic = _pausedTopics.Count > 0 ? _pausedTopics.Peek() : null;
                if (callingTopic != null) {
                    _pendingSubTopics[e.TopicName] = new PendingSubTopic {
                        CallingTopic = callingTopic,
                        SubTopic = nextFlow,
                        CallingTopicName = callingTopic.Name,
                        SubTopicName = e.TopicName,
                        StartTime = DateTime.UtcNow
                    };

                    _logger.LogInformation("[InsuranceAgentService] Registered sub-topic '{SubTopic}' for completion tracking", e.TopicName);
                }
                else {
                    _logger.LogWarning("[InsuranceAgentService] WaitForCompletion=true but no calling topic found in paused stack");
                }
            }

            // ───────────────────────────────────────────────
            // 🚀 Run the next topic
            // ───────────────────────────────────────────────
            if (e.TopicName == "MarketingT1Topic")
                Console.WriteLine($"[CRITICAL DEBUG] About to call MarketingTypeOneTopic.RunAsync()");

            var result = await nextFlow.RunAsync();

            if (e.TopicName == "MarketingT1Topic")
                Console.WriteLine($"[CRITICAL DEBUG] MarketingTypeOneTopic.RunAsync() completed with result: IsCompleted={result.IsCompleted}");

            _logger.LogInformation("[InsuranceAgentService] Topic completed. WaitForCompletion: {WaitForCompletion}, IsCompleted: {IsCompleted}",
                waitForCompletion, result.IsCompleted);

            // ───────────────────────────────────────────────
            // 🧩 Completion logic
            // ───────────────────────────────────────────────
            if (!waitForCompletion) {
                _logger.LogInformation("[InsuranceAgentService] Legacy hand-off to {TopicName} completed", e.TopicName);

                if (result.IsCompleted && !string.IsNullOrEmpty(result.NextTopicName)) {
                    _logger.LogInformation("[InsuranceAgentService] Legacy topic completed with next topic: {NextTopic}", result.NextTopicName);
                }
            }
            else if (waitForCompletion && result.IsCompleted) {
                _logger.LogInformation("[InsuranceAgentService] Sub-topic completed immediately, calling HandleSubTopicCompletion for {TopicName}", e.TopicName);
                _pendingSubTopics.Remove(e.TopicName);
                await HandleSubTopicCompletion(nextFlow, result);
            }
            else if (waitForCompletion) {
                _logger.LogInformation("[InsuranceAgentService] Sub-topic '{SubTopic}' started, waiting for completion event", e.TopicName);
            }
        }
        else {
            _logger.LogWarning("[InsuranceAgentService] Triggered topic {TopicName} not found or not a TopicFlow.", e.TopicName);
        }
    }



    // NEW: Handle custom events from EventTriggerActivity
    private async void OnCustomEventTriggered(object? sender, CustomEventTriggeredEventArgs e) {
        var senderType = sender?.GetType().Name ?? "Unknown";
        var senderId = sender is TopicFlowActivity activity ? activity.Id : "Unknown";

        Console.WriteLine($"[InsuranceAgentService.OnCustomEventTriggered] *** EVENT RECEIVED *** CustomEvent '{e.EventName}' from sender '{senderId}' ({senderType}) with data: {e.EventData}");
        _logger.LogInformation("[InsuranceAgentService] Custom event triggered: {EventName} from {SenderId}", e.EventName, senderId);

        // Forward the event to HybridChatService
        CustomEventTriggered?.Invoke(this, e);

        // For blocking events, provide a response
        if (e.WaitForResponse && sender is EventTriggerActivity triggerActivity) {
            // Simulate processing time for demo
            await Task.Delay(100);

            // Provide a generic response - could be enhanced with specific event handling
            var response = new {
                success = true,
                message = $"Event '{e.EventName}' processed successfully",
                timestamp = DateTime.UtcNow,
                processedBy = "InsuranceAgentService"
            };

            Console.WriteLine($"[InsuranceAgentService.OnCustomEventTriggered] Providing response for blocking event: {response}");
            triggerActivity.HandleUIResponse(e.Context, response);
        }
    }

    private async void OnTopicLifecycleChanged(object? sender, TopicLifecycleEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] TopicLifecycleChanged: {TopicName} -> {State}", e.TopicName, e.State);

        try {
            // ───────────────────────────────────────────────
            // 🧩 Handle sub-topic completion
            // ───────────────────────────────────────────────
            if (e.State == TopicLifecycleState.Completed && _pendingSubTopics.ContainsKey(e.TopicName)) {
                // ✅ Guard against duplicate resume:
                // If the stack handler already popped the parent, skip this duplicate.
                if (_pausedTopics.Count == 0) {
                    _logger.LogDebug(
                        "[InsuranceAgentService] Skipping duplicate OnTopicLifecycleChanged for {TopicName} (already resumed by stack handler).",
                        e.TopicName);
                    return;
                }

                var pendingSubTopic = _pendingSubTopics[e.TopicName];
                _pendingSubTopics.Remove(e.TopicName);

                _logger.LogInformation(
                    "[InsuranceAgentService] Sub-topic '{SubTopic}' completed via lifecycle event, resuming '{CallingTopic}'",
                    e.TopicName, pendingSubTopic.CallingTopicName);

                // Create synthetic result and delegate to unified resumption path
                var subTopicResult = TopicResult.CreateCompleted("Sub-topic completed", pendingSubTopic.SubTopic.Context);
                await HandleSubTopicCompletion(pendingSubTopic.SubTopic, subTopicResult);
                return;
            }

            // ───────────────────────────────────────────────
            // 💬 Handle FallbackTopic completion
            // ───────────────────────────────────────────────
            if (e.State == TopicLifecycleState.Completed && e.TopicName == "FallbackTopic") {
                _logger.LogInformation("[InsuranceAgentService] 🧭 FallbackTopic completed — attempting to resume paused topic.");

                var fallbackTopic = _topicRegistry.GetTopic("FallbackTopic") as TopicFlow;
                if (fallbackTopic == null) {
                    _logger.LogWarning("[InsuranceAgentService] FallbackTopic not found in registry during completion handling.");
                    return;
                }

                var fallbackResult = TopicResult.CreateCompleted("Fallback completed", fallbackTopic.Context);
                await HandleFallbackCompletion(fallbackTopic, fallbackResult);
                return;
            }

            // ───────────────────────────────────────────────
            // 💤 Other transitions
            // ───────────────────────────────────────────────
            _logger.LogDebug("[InsuranceAgentService] Topic '{TopicName}' transitioned to {State} (no action taken)",
                e.TopicName, e.State);
        } catch (Exception ex) {
            _logger.LogError(ex,
                "[InsuranceAgentService] Error processing TopicLifecycleChanged for topic '{TopicName}'",
                e.TopicName);
        }
    }

    public event EventHandler<CardStateChangedEventArgs>? CardStateChanged;

    /// <summary>
    /// Helper to raise a UI event when an Adaptive Card changes its active/readonly state.
    /// </summary>
    private void OnCardStateChanged(string cardId, CardState newState) {
        _logger.LogInformation("[InsuranceAgentService] Card {CardId} state changed -> {State}", cardId, newState);
        CardStateChanged?.Invoke(this, new CardStateChangedEventArgs(cardId, newState));
    }

    /// <summary>
    /// Event args for card state changes (sent to the chat UI).
    /// </summary>
    public class CardStateChangedEventArgs : EventArgs {
        public string CardId { get; }
        public CardState State { get; }

        public CardStateChangedEventArgs(string cardId, CardState state) {
            CardId = cardId;
            State = state;
        }
    }

    // ---- BOOL→STRING helpers for readability
    private static bool IsYes(TopicWorkflowContext ctx, string key) {
        var value = ctx.GetValue<object?>(key);

        // Handle null
        if (value is null)
            return false;

        // Handle JsonElement (common when context is populated from serialized JSON)
        if (value is JsonElement jsonElement) {
            // Try to unwrap underlying string or boolean
            if (jsonElement.ValueKind == JsonValueKind.String)
                value = jsonElement.GetString();
            else if (jsonElement.ValueKind == JsonValueKind.True)
                return true;
            else if (jsonElement.ValueKind == JsonValueKind.False)
                return false;
            else
                return false;
        }

        // Handle boolean or string values
        return value switch {
            bool b => b,
            string s => s.Trim().ToLowerInvariant() switch {
                "yes" or "y" or "true" or "1" => true,
                _ => false
            },
            _ => false
        };
    }

    private static bool IsNo(TopicWorkflowContext ctx, string key) {
        var value = ctx.GetValue<object?>(key);

        return value switch {
            // Already a boolean
            bool b => !b,

            // Normalize strings
            string s => s.Trim().ToLowerInvariant() switch {
                "no" or "n" or "false" or "0" => true,
                _ => false
            },

            // Null or unrecognized type
            _ => false
        };
    }

    private static bool IsUnknown(TopicWorkflowContext ctx, string key)
        => ctx.GetValue<bool?>(key) is null;

    // ---- Simple factory for "no-op" skip branch
    private TopicFlowActivity IfCase(string id, Func<TopicWorkflowContext, bool> condition, TopicFlowActivity activity) {
        return ConditionalActivity<TopicFlowActivity>.If(
            id,
            condition,
            (yesId, ctx) => activity,
            (noId, ctx) => Skip(id));
    }

    private static TopicFlowActivity Skip(string id)
        => new SimpleActivity($"{id}_SKIP", (c, d) => Task.FromResult<object?>(null));

    private TopicFlowActivity ToMarketingT1Topic(string id = "ToMarketingT1") {
        _logger?.LogInformation($"[DEBUG] ToMarketingT1Topic invoked → id={id}, triggering 'MarketingT1Topic' (waitForCompletion=false)");

        return new TriggerTopicActivity(
            id,
            "MarketingT1Topic",
            _logger,
            waitForCompletion: false,
            conversationContext: _context
        );
    }


    private TopicFlowActivity ToMarketingT2Topic(string id = "ToMarketingT2") {
        _logger?.LogInformation($"[DEBUG] ToMarketingT2Topic invoked → id={id}, triggering 'MarketingT2Topic' (waitForCompletion=false)");

        return new TriggerTopicActivity(
            id,
            "MarketingT2Topic",
            _logger,
            waitForCompletion: false,
            conversationContext: _context
        );
    }

    private TopicFlowActivity AskCaliforniaResidency(string id, TopicWorkflowContext context) {
        _logger.LogInformation("[AskCaliforniaResidency] 🔧 Constructing card activity with ID='{Id}'", id);

        var cardActivity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
            id,
            context,  // ✅ Use the passed-in context, not the outer _wfContext
            cardFactory: card => {
                _logger.LogInformation("[AskCaliforniaResidency] 🧩 Card factory invoked for ID='{Id}'", id);

                var result = card.Create(isResident: true, zip_code: context.GetValue<string>("zip_code"), ccpa_acknowledgment: context.GetValue<string?>("ccpa_acknoledgement"));

                // Safe cast to AdaptiveCardModel if available
                if (result is AdaptiveCardModel model) {
                    _logger.LogInformation(
                        "[AskCaliforniaResidency] ✅ Card created (BodyCount={BodyCount}, Actions={ActionsCount})",
                        model.Body?.Count ?? 0,
                        model.Actions?.Count ?? 0);
                }
                else {
                    _logger.LogWarning(
                        "[AskCaliforniaResidency] ⚠️ Card factory returned unexpected type: {Type}",
                        result?.GetType().Name ?? "null");
                }

                return result;
            });

        _logger.LogInformation("[AskCaliforniaResidency] 🚀 Returning AdaptiveCardActivity '{Id}' to flow engine", id);

        return cardActivity;
    }

    private List<TopicFlowActivity>? ComplianceFlowActivities() {
        return new List<TopicFlowActivity> {
// ───────────────────────────────────────────────
// ✅ TCPA = YES
// ───────────────────────────────────────────────
        IfCase("TCPA_YES_BRANCH", ctx =>
            IsYes(ctx, "tcpa_consent"),
            ConditionalActivity<TopicFlowActivity>.If(
                "HAS_CA_INFO_YES_TCPA",
                c => IsYes(c, "is_california_resident"),

                // ────────── California Resident ──────────
                (id, c) => new CompositeActivity("ASK_CCPA_YES_CA", new List<TopicFlowActivity> {
                    AskCaliforniaResidency("CA_CARD_YES_CA", c), // ask for CCPA consent

                    // ✅ FIXED CONDITIONAL
                    ConditionalActivity<TopicFlowActivity>.If(
                        "HAS_CCPA_ACK",
                        cc =>
                        {
                            return IsYes(cc, "ccpa_acknowledgment");
                        },
                        // TRUE → Full marketing (T1)
                        (id2, cc) => ToMarketingT1Topic("AFTER_CCPA_YES"),
                        // FALSE → Limited informational (T2)
                        (id2, cc) => ToMarketingT2Topic("AFTER_CCPA_NO")
                    )
                }),

                // ────────── Non-California ──────────
                (id, c) => ToMarketingT1Topic("NON_CA_TCPA_YES")
            )
        ),


        // ───────────────────────────────────────────────
        // 🚫 TCPA = NO
        // ───────────────────────────────────────────────
        IfCase("TCPA_NO", ctx =>
            IsNo(ctx, "tcpa_consent"),
            new TriggerTopicActivity(
                "TO_MARKETING_T3_AFTER_TCPA_NO",
                "MarketingT3Topic",                                 // restricted informational path
                _logger,
                waitForCompletion: false,
                conversationContext: _context
            )
        )
    };
    }

}

