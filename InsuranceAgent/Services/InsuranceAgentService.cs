using ConversaCore.StateMachine;
using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Topics;
using InsuranceAgent.Models;
using InsuranceAgent.Cards;
using InsuranceAgent.Topics;
using InsuranceAgent.Topics.CaliforniaResidentTopic;
using System.ComponentModel.DataAnnotations;

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

        if (topic == null) {
            _logger.LogWarning("No topic could handle '{Message}'", userMessage);
            MatchingTopicNotFound?.Invoke(this, new MatchingTopicNotFoundEventArgs(userMessage));
            return;
        }

        if (topic is TopicFlow flow) {
            // Unhook events from previous topic before switching
            if (_activeTopic is TopicFlow currentActiveTopic) {
                UnhookTopicEvents(currentActiveTopic);
            }

            _activeTopic = flow;
            HookTopicEvents(flow);

            _logger.LogInformation("Activated topic {TopicName} (confidence {Confidence:P2})",
                flow.Name, confidence);

            await flow.RunAsync(ct);
        }
        else {
            _logger.LogWarning("Resolved topic is not a TopicFlow: {Type}", topic.GetType().Name);
        }
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

    private void HookTopicEvents(TopicFlow flow) {
        flow.TopicLifecycleChanged += (s, e) => TopicLifecycleChanged?.Invoke(this, e);
        flow.TopicLifecycleChanged += OnTopicLifecycleChanged; // For event-driven completion tracking
        flow.ActivityCreated += OnActivityCreated;

        flow.ActivityCompleted += (s, e) => {
            _logger.LogInformation("[InsuranceAgentService] ActivityCompleted -> {ActivityId}", e.ActivityId);
            ActivityCompleted?.Invoke(this, e);
        };

        flow.TopicInserted += (s, e) => TopicInserted?.Invoke(this, e);

        foreach (var act in flow.GetAllActivities()) {
            act.ActivityLifecycleChanged += (s, e) => {
                _logger.LogInformation("[InsuranceAgentService] ActivityLifecycleChanged: {ActivityId} -> {State} | Data={Data}",
                    e.ActivityId, e.State, e.Data);
                ActivityLifecycleChanged?.Invoke(this, e);
            };

            // Handle adaptive cards
            if (act is IAdaptiveCardActivity cardAct) {
                cardAct.CardJsonSent += (s, e) => {
                    _logger.LogInformation("[InsuranceAgentService] CardJsonSent {CardId}", e.CardId);
                    ActivityAdaptiveCardReady?.Invoke(this,
                        new ActivityAdaptiveCardEventArgs(e.CardJson ?? "{}", e.CardId, e.RenderMode));
                };

                cardAct.ValidationFailed += (s, e) => {
                    _logger.LogWarning("[InsuranceAgentService] ValidationFailed: {Error}", e.Exception?.Message);
                };

                cardAct.CardJsonEmitted += (s, e) =>
                    _logger.LogDebug("[InsuranceAgentService] CardJsonEmitted (internal)");
                cardAct.CardJsonSending += (s, e) =>
                    _logger.LogDebug("[InsuranceAgentService] CardJsonSending (internal)");
                cardAct.CardJsonRendered += (s, e) =>
                    _logger.LogDebug("[InsuranceAgentService] CardJsonRendered (client ack)");
                cardAct.CardDataReceived += (s, e) =>
                    _logger.LogDebug("[InsuranceAgentService] CardDataReceived (internal)");
                cardAct.ModelBound += (s, e) =>
                    _logger.LogDebug("[InsuranceAgentService] ModelBound (internal)");
            }

            // Handle topic triggers
            if (act is ITopicTriggeredActivity trigger) {
                Console.WriteLine($"[InsuranceAgentService.HookTopicEvents] *** SUBSCRIPTION *** Subscribing to TopicTriggered events from activity '{act.Id}' ({act.GetType().Name})");
                trigger.TopicTriggered += OnTopicTriggered;
            }

            // Handle topic triggers from ConditionalActivity containers
            // (child TriggerTopicActivity is created dynamically, so we subscribe to the container's forwarded event)
            if (act is ConditionalActivity<TriggerTopicActivity> conditional) {
                Console.WriteLine($"[InsuranceAgentService.HookTopicEvents] *** SUBSCRIPTION *** Subscribing to TopicTriggered events from ConditionalActivity '{act.Id}'");
                conditional.TopicTriggered += OnTopicTriggered;
            }

            // CRITICAL FIX: Handle topic triggers from CompositeActivity containers
            // (child activities like TriggerTopicActivity are created dynamically in CompositeActivity, so we need to subscribe to the container's forwarded event)
            if (act is CompositeActivity composite) {
                Console.WriteLine($"[InsuranceAgentService.HookTopicEvents] *** SUBSCRIPTION *** Subscribing to TopicTriggered events from CompositeActivity '{act.Id}'");
                composite.TopicTriggered += OnTopicTriggered;
            }
        }
    }

    private void UnhookTopicEvents(TopicFlow flow) {
        // Unhook topic-level events
        flow.TopicLifecycleChanged -= (s, e) => TopicLifecycleChanged?.Invoke(this, e);
        flow.TopicLifecycleChanged -= OnTopicLifecycleChanged;
        flow.ActivityCreated -= OnActivityCreated;
        flow.ActivityCompleted -= (s, e) => {
            _logger.LogInformation("[InsuranceAgentService] ActivityCompleted -> {ActivityId}", e.ActivityId);
            ActivityCompleted?.Invoke(this, e);
        };
        flow.TopicInserted -= (s, e) => TopicInserted?.Invoke(this, e);

        // Unhook activity-level events
        foreach (var act in flow.GetAllActivities()) {
            if (act is TriggerTopicActivity trigger) {
                trigger.TopicTriggered -= OnTopicTriggered;
            }

            if (act is ConditionalActivity<TriggerTopicActivity> conditional) {
                conditional.TopicTriggered -= OnTopicTriggered;
            }

            // CRITICAL FIX: Unhook CompositeActivity TopicTriggered events
            if (act is CompositeActivity composite) {
                Console.WriteLine($"[InsuranceAgentService.UnhookTopicEvents] *** UNSUBSCRIPTION *** Unsubscribing from TopicTriggered events from CompositeActivity '{act.Id}'");
                composite.TopicTriggered -= OnTopicTriggered;
            }
        }
    }

    private void OnActivityCreated(object? sender, ActivityCreatedEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] ActivityCreated -> {PayloadType}: {Content}",
            e.Content?.GetType().Name, e.Content);

        switch (e.Content) {
            case string msg:
                RaiseMessage(msg);
                break;
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

        // Add compliance trigger
        flow.Add(new TriggerTopicActivity(ActivityId_CollectCompliance, "ComplianceTopic", _logger, waitForCompletion: true));

        // Add a simple activity to process the compliance data before continuing
        flow.Add(new SimpleActivity(ActivityId_ProcessComplianceData, (ctx, input) => {
            _logger.LogInformation("[InsuranceAgentService] Processing compliance data before decision tree");
            // No additional processing needed, just act as a buffer between topic completion and decision tree
            return Task.FromResult<object?>(null);
        }));

        // Add the complex compliance flowchart
        flow.AddRange(
            ComplianceFlowActivities()
            );

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
        _logger.LogInformation("[InsuranceAgentService] Resetting conversation");

        // Clear all state
        _pausedTopics.Clear();
        _pendingSubTopics.Clear();

        // Unhook current topic events
        if (_activeTopic is TopicFlow currentTopic) {
            UnhookTopicEvents(currentTopic);
        }
        _activeTopic = null;

        // Clear contexts
        _context.Reset();
        _wfContext.Clear();

        // --- FULL TOPIC STATE RESET ---
        // Reset ALL topics to ensure clean state
        var allTopics = _topicRegistry.GetAllTopics();
        foreach (var topic in allTopics) {
            if (topic is TopicFlow topicFlow) {
                _logger.LogInformation("[InsuranceAgentService] Resetting topic: {TopicName}", topic.Name);
                topicFlow.Reset();
            }
        }

        // --- SPECIAL HANDLING FOR CONVERSATION START TOPIC ---
        // This is critical for properly restarting the conversation
        var conversationStartTopic = _topicRegistry.GetTopic("ConversationStart") as TopicFlow;
        if (conversationStartTopic != null) {
            // Clear both context flags to be thorough
            conversationStartTopic.Context.SetValue("ConversationStartTopic.HasRun", false);
            _context.SetValue("ConversationStartTopic.HasRun", false);

            // Force clear any other related flags that might prevent restart
            _context.SetValue("Global_ConversationActive", false);
            _context.SetValue("Global_TopicHistory", new List<string>());
            _context.SetValue("Global_UserInteractionCount", 0);

            // Ensure state machine is properly reset
            // Force ConversationStartTopic to Idle state to fix issues with multiple resets
            // This uses reflection to access the protected _fsm field
            var stateMachine = conversationStartTopic.GetType().BaseType?.GetField("_fsm",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance)?.GetValue(conversationStartTopic);

            if (stateMachine is ITopicStateMachine<ConversaCore.TopicFlow.TopicFlow.FlowState> fsm) {
                // Use our new ForceState method to guarantee proper state
                fsm.ForceState(ConversaCore.TopicFlow.TopicFlow.FlowState.Idle,
                    "Forced reset to Idle in ResetConversationAsync");

                // Also clear transition history for a clean slate
                fsm.ClearTransitionHistory();

                _logger.LogInformation("[InsuranceAgentService] ConversationStartTopic state machine forced to Idle state");
            }

            // CRITICAL: Don't add domain activities here, they will be added in StartConversationAsync
            // Removed: AddDomainActivitiesToStartTopic(conversationStartTopic);

            // CRITICAL FIX: Also force ComplianceTopic to Idle state and clear all its flags
            var complianceTopic = _topicRegistry.GetTopic("ComplianceTopic") as TopicFlow;
            if (complianceTopic != null) {
                // Force reset state machine
                var complianceStateMachine = complianceTopic.GetType().BaseType?.GetField("_fsm",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)?.GetValue(complianceTopic);

                if (complianceStateMachine is ITopicStateMachine<ConversaCore.TopicFlow.TopicFlow.FlowState> complianceFsm) {
                    complianceFsm.ForceState(ConversaCore.TopicFlow.TopicFlow.FlowState.Idle,
                        "Forced reset to Idle in ResetConversationAsync");
                    complianceFsm.ClearTransitionHistory();
                    _logger.LogInformation("[InsuranceAgentService] ComplianceTopic state machine forced to Idle state");
                }

                // Clear any activity flags
                _wfContext.SetValue("ShowComplianceCard_Sent", null);
                _wfContext.SetValue("ShowComplianceCard_Rendered", null);
                _wfContext.SetValue("ShowComplianceCard_Completed", null);
                _wfContext.SetValue("ComplianceTopic_Completed", null);
                _wfContext.SetValue("ComplianceTopic_HasRun", null);

                // Clear out any compliance data to force a fresh card
                // Set empty/default values instead of trying to remove keys
                _context.SetValue("compliance_data", new Dictionary<string, object>());
                _context.SetValue("tcpa_consent", false);
                _context.SetValue("ccpa_acknowledgment", false);

                _logger.LogInformation("[InsuranceAgentService] ComplianceTopic activity flags and compliance data cleared");
            }

            _logger.LogInformation("[InsuranceAgentService] ConversationStartTopic.HasRun flag cleared and conversation flags reset");
        }

        // Log completion of reset to help with debugging
        _logger.LogInformation("[InsuranceAgentService] Conversation reset completed - all topics and context cleared");

        // Fire reset event for UI
        ConversationReset?.Invoke(this, new ConversationResetEventArgs());

        // Add a small delay to ensure UI has time to process the reset
        await Task.Delay(100, ct);

        // Start fresh conversation
        await StartConversationAsync(new ChatSessionState(), ct);
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
            // Copy relevant data from the sub-topic's workflow context
            foreach (var key in subTopicResult.wfContext.GetKeys()) {
                var value = subTopicResult.wfContext.GetValue<object>(key);
                if (value != null) {
                    completionData[key] = value;
                }
            }
        }

        _wfContext.SetValue("SubTopicCompletionData", completionData);

        // Pop the topic call from conversation context
        var callInfo = _context.PopTopicCall(completionData);
        if (callInfo != null) {
            _wfContext.SetValue("ResumeData", callInfo.ResumeData);
            _logger.LogInformation("[InsuranceAgentService] Call info retrieved: {CallingTopic} -> {SubTopic}",
                callInfo.CallingTopicName, callInfo.SubTopicName);
        }

        // Resume the calling topic
        // Unhook events from current active topic before switching
        if (_activeTopic is TopicFlow currentActiveTopic) {
            UnhookTopicEvents(currentActiveTopic);
        }

        _activeTopic = callingTopic;
        HookTopicEvents(callingTopic);

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
        var senderId = sender is TopicFlowActivity activity ? activity.Id : "Unknown";
        
        Console.WriteLine($"[InsuranceAgentService.OnTopicTriggered] *** EVENT RECEIVED *** TopicTriggered event received from sender '{senderId}' ({senderType}) for topic '{e.TopicName}'");
        
        // Extract WaitForCompletion from the TriggerTopicActivity sender
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

            // Guard against duplicate topic activation
            if (_activeTopic != null && _activeTopic.Name == e.TopicName) {
                _logger.LogWarning("[InsuranceAgentService] Topic '{TopicName}' is already active, ignoring duplicate trigger", e.TopicName);
                Console.WriteLine($"[InsuranceAgentService.OnTopicTriggered] DUPLICATE IGNORED: Topic '{e.TopicName}' is already active");
                return;
            }

            if (waitForCompletion) {
                // NEW: Sub-topic pattern - pause current topic
                if (_activeTopic is TopicFlow currentFlow) {
                    _logger.LogInformation("[InsuranceAgentService] Pausing topic '{CurrentTopic}' for sub-topic '{SubTopic}'",
                        currentFlow.Name, nextFlow.Name);
                    _pausedTopics.Push(currentFlow);
                }
            }

            // Unhook events from previous topic before switching
            if (_activeTopic is TopicFlow currentActiveTopic) {
                UnhookTopicEvents(currentActiveTopic);
            }

            _activeTopic = nextFlow;
            HookTopicEvents(nextFlow);

            if (waitForCompletion) {
                // Get the calling topic from paused topics stack
                var callingTopic = _pausedTopics.Count > 0 ? _pausedTopics.Peek() : null;
                if (callingTopic != null) {
                    // Register for event-driven completion tracking
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

            var result = await nextFlow.RunAsync();

            _logger.LogInformation("[InsuranceAgentService] Sub-topic completed. WaitForCompletion: {WaitForCompletion}, IsCompleted: {IsCompleted}",
                waitForCompletion, result.IsCompleted);

            // Only handle immediate completion for legacy topics or actually completed topics
            if (!waitForCompletion && result.IsCompleted) {
                _logger.LogInformation("[InsuranceAgentService] Calling HandleSubTopicCompletion for legacy topic {TopicName}", e.TopicName);
                await HandleSubTopicCompletion(nextFlow, result);
            }
            else if (waitForCompletion && result.IsCompleted) {
                _logger.LogInformation("[InsuranceAgentService] Sub-topic completed immediately, calling HandleSubTopicCompletion for {TopicName}", e.TopicName);
                // Remove from pending since it completed immediately
                _pendingSubTopics.Remove(e.TopicName);
                await HandleSubTopicCompletion(nextFlow, result);
            }
            else if (waitForCompletion) {
                _logger.LogInformation("[InsuranceAgentService] Sub-topic '{SubTopic}' started, waiting for completion event", e.TopicName);
            }
            // For WaitForCompletion=true and IsCompleted=false, the TopicLifecycleChanged event will handle completion
        }
        else {
            _logger.LogWarning("[InsuranceAgentService] Triggered topic {TopicName} not found or not a TopicFlow.", e.TopicName);
        }
    }

    private async void OnTopicLifecycleChanged(object? sender, TopicLifecycleEventArgs e) {
        _logger.LogInformation("[InsuranceAgentService] TopicLifecycleChanged: {TopicName} -> {State}", e.TopicName, e.State);

        // Handle sub-topic completion
        if (e.State == TopicLifecycleState.Completed && _pendingSubTopics.ContainsKey(e.TopicName)) {
            var pendingSubTopic = _pendingSubTopics[e.TopicName];
            _pendingSubTopics.Remove(e.TopicName);

            _logger.LogInformation("[InsuranceAgentService] Sub-topic '{SubTopic}' completed via lifecycle event, resuming '{CallingTopic}'",
                e.TopicName, pendingSubTopic.CallingTopicName);

            // Create a synthetic TopicResult for the completed sub-topic
            var subTopicResult = TopicResult.CreateCompleted("Sub-topic completed", pendingSubTopic.SubTopic.Context);

            await HandleSubTopicCompletion(pendingSubTopic.SubTopic, subTopicResult);
        }
    }

    // ---- BOOL→STRING helpers for readability
    private static bool IsYes(TopicWorkflowContext ctx, string key)
        => ctx.GetValue<bool?>(key) == true;
    private static bool IsNo(TopicWorkflowContext ctx, string key)
        => ctx.GetValue<bool?>(key) == false;
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

    private TopicFlowActivity ToMarketingT1Topic(string id = "ToMarketingT1") =>
        new TriggerTopicActivity(id,
            "MarketingTypeOneTopic",
            _logger,
            waitForCompletion: false,
            conversationContext: _context);

    private TopicFlowActivity ToInfoTopic(string id = "ToInfo") =>
        new TriggerTopicActivity(id,
            "InformationalContentTopic",
            _logger,
            waitForCompletion: false,
            conversationContext: _context);

    private TopicFlowActivity ToBasicNavTopic(string id = "ToBasicNav") =>
        new TriggerTopicActivity(id,
            "BasicNavigationTopic",
            _logger,
            waitForCompletion: false,
            conversationContext: _context);

    private TopicFlowActivity AskCaliforniaResidency(string id, string nextTopic, string marketingPath) {
        return new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
            id,
            _wfContext,
            cardFactory: c => c.Create(),
            onTransition: (from, to, data) => {
                if (to == ActivityState.Completed && data is CaliforniaResidentModel m) {
                    bool isCA = m.IsCaliforniaResident ?? false;
                    bool zipOK = m.HasValidCaliforniaZip();
                    _wfContext.SetValue("is_california_resident", isCA);
                    _wfContext.SetValue("has_valid_ca_zip", zipOK);

                    if (isCA && zipOK)
                        _wfContext.SetValue("marketing_path", marketingPath + "_ca");
                    else
                        _wfContext.SetValue("marketing_path", marketingPath + "_nonca");
                    _wfContext.SetValue("next_topic", nextTopic);
                }
            });
    }

    private List<TopicFlowActivity>? ComplianceFlowActivities() {
        return new List<TopicFlowActivity> {
            // ───────────── TCPA YES ─────────────
            IfCase("TCPA_YES_CCPA_YES", ctx =>
                IsYes(ctx,"tcpa_consent") && IsYes(ctx,"ccpa_acknowledgment"),
                ConditionalActivity<TopicFlowActivity>.If(
                    "HAS_CA_INFO_YES_YES",
                    c => c.GetValue<bool?>("is_california_resident").HasValue,
                    (id,c) => ToMarketingT1Topic("CA_KNOWN_YES_YES"),
                    (id,c) => new CompositeActivity("ASK_CA_YES_YES", new List<TopicFlowActivity>{
                        AskCaliforniaResidency("CA_CARD_YES_YES","MarketingTypeOneTopic","full_with_ca_protection"),
                        ToMarketingT1Topic("AFTER_CA_YES_YES")
                    }))
            ),

            IfCase("TCPA_YES_CCPA_NO", ctx =>
                IsYes(ctx,"tcpa_consent") && IsNo(ctx,"ccpa_acknowledgment"),
                ToMarketingT1Topic("YES_NO_DIRECT")),

            IfCase("TCPA_YES_CCPA_UNKNOWN", ctx =>
                IsYes(ctx,"tcpa_consent") && IsUnknown(ctx,"ccpa_acknowledgment"),
                ConditionalActivity<TopicFlowActivity>.If(
                    "HAS_CA_INFO_YES_UNKNOWN",
                    c => c.GetValue<bool?>("is_california_resident").HasValue,
                    // Already know residency
                    (id,c) => new SimpleActivity(id,(cc,dd)=>{
                        bool isCA = cc.GetValue<bool?>("is_california_resident")==true;
                        cc.SetValue("marketing_path", isCA ? "marketing_with_ca_protection" : "marketing_optional_disclosure");
                        cc.SetValue("next_topic","LeadQualificationTopic");
                        return Task.FromResult<object?>(null);
                    }),
                    // Ask for residency
                    (id,c) => new CompositeActivity("ASK_CA_YES_UNKNOWN", new List<TopicFlowActivity>{
                        AskCaliforniaResidency("CA_CARD_YES_UNKNOWN","MarketingTypeOneTopic","marketing_with_ca_protection"),
                        ToMarketingT1Topic("AFTER_CA_YES_UNKNOWN")
                    }))
            ),

            // ───────────── TCPA NO ─────────────
            IfCase("TCPA_NO_CCPA_YES", ctx =>
                IsNo(ctx,"tcpa_consent") && IsYes(ctx,"ccpa_acknowledgment"),
                ConditionalActivity<TopicFlowActivity>.If(
                    "HAS_CA_INFO_NO_YES",
                    c => c.GetValue<bool?>("is_california_resident").HasValue,
                    (id,c)=> new SimpleActivity(id,(cc,dd)=>{
                        bool isCA = cc.GetValue<bool?>("is_california_resident")==true;
                        cc.SetValue("marketing_path", isCA?"no_marketing_ca_disclosure":"no_marketing_optional");
                        cc.SetValue("next_topic","InformationalContentTopic");
                        return Task.FromResult<object?>(null);
                    }),
                    (id,c)=> new CompositeActivity("ASK_CA_NO_YES", new List<TopicFlowActivity>{
                        AskCaliforniaResidency("CA_CARD_NO_YES","InformationalContentTopic","no_marketing"),
                        ToInfoTopic("AFTER_CA_NO_YES")
                    }))
            ),

            IfCase("TCPA_NO_CCPA_NO", ctx =>
                IsNo(ctx, "tcpa_consent") && IsNo(ctx, "ccpa_acknowledgment"),
                new CompositeActivity("NO_NO_BLOCKED_SEQUENCE", new List<TopicFlowActivity> {
                    new SimpleActivity("NO_NO_BLOCKED", (c, d) => {
                        c.SetValue("marketing_path", "blocked_minimal");
                        c.SetValue("next_topic", "BasicNavigationTopic");
                        return Task.FromResult<object?>(null);
                    }),
                    ToBasicNavTopic("AFTER_NO_NO")
                })
            ),

            IfCase("TCPA_NO_CCPA_UNKNOWN", ctx =>
                IsNo(ctx,"tcpa_consent") && IsUnknown(ctx,"ccpa_acknowledgment"),
                ConditionalActivity<TopicFlowActivity>.If(
                    "HAS_CA_INFO_NO_UNKNOWN",
                    c => c.GetValue<bool?>("is_california_resident").HasValue,
                    (id,c)=> new SimpleActivity(id,(cc,dd)=>{
                        bool isCA = cc.GetValue<bool?>("is_california_resident")==true;
                        cc.SetValue("marketing_path", isCA?"no_marketing_mandatory_ca":"no_marketing_minimal");
                        cc.SetValue("next_topic","InformationalContentTopic");
                        return Task.FromResult<object?>(null);
                    }),
                    (id,c)=> new CompositeActivity("ASK_CA_NO_UNKNOWN", new List<TopicFlowActivity>{
                        AskCaliforniaResidency("CA_CARD_NO_UNKNOWN","InformationalContentTopic","no_marketing"),
                        ToInfoTopic("AFTER_CA_NO_UNKNOWN")
                    }))
            ),

            // ───────────── TCPA UNKNOWN ─────────────
            IfCase("TCPA_UNKNOWN_CCPA_YES", ctx =>
                IsUnknown(ctx,"tcpa_consent") && IsYes(ctx,"ccpa_acknowledgment"),
                new CompositeActivity("CA_UNKNOWN_YES", new List<TopicFlowActivity>{
                    AskCaliforniaResidency("CA_CARD_UNKNOWN_YES","InformationalContentTopic","no_marketing_tcpa_risk"),
                    ToInfoTopic("AFTER_CA_UNKNOWN_YES")
                })
            ),

            IfCase("TCPA_UNKNOWN_CCPA_NO", ctx =>
                IsUnknown(ctx, "tcpa_consent") && IsNo(ctx, "ccpa_acknowledgment"),
                new CompositeActivity("UNKNOWN_NO_SEQUENCE", new List<TopicFlowActivity> {
                    new SimpleActivity("UNKNOWN_NO", (c, d) => {
                        c.SetValue("marketing_path", "maximum_restriction");
                        c.SetValue("next_topic", "BasicNavigationTopic");
                        return Task.FromResult<object?>(null);
                    }),
                    ToBasicNavTopic("AFTER_UNKNOWN_NO")
                })
            ),

            IfCase("TCPA_UNKNOWN_CCPA_UNKNOWN", ctx =>
                IsUnknown(ctx,"tcpa_consent") && IsUnknown(ctx,"ccpa_acknowledgment"),
                new CompositeActivity("CA_UNKNOWN_UNKNOWN", new List<TopicFlowActivity>{
                    AskCaliforniaResidency("CA_CARD_UNKNOWN_UNKNOWN","BasicNavigationTopic","conservative"),
                    ToBasicNavTopic("AFTER_CA_UNKNOWN_UNKNOWN")
                })
            )
        };
    }
}
