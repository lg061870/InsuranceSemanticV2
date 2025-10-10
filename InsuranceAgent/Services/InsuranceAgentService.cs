using ConversaCore.StateMachine;
using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using ConversaCore.Topics;
using InsuranceAgent.Models;

public class InsuranceAgentService {
    private readonly TopicRegistry _topicRegistry;
    private readonly IConversationContext _context;
    private readonly TopicWorkflowContext _wfContext;
    private readonly ILogger<InsuranceAgentService> _logger;

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
            if (act is TriggerTopicActivity trigger) {
                trigger.TopicTriggered += OnTopicTriggered;
            }

            // Handle topic triggers from ConditionalActivity containers
            // (child TriggerTopicActivity is created dynamically, so we subscribe to the container's forwarded event)
            if (act is ConditionalActivity<TriggerTopicActivity> conditional) {
                conditional.TopicTriggered += OnTopicTriggered;
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

    public async Task StartConversationAsync(ChatSessionState sessionState, CancellationToken ct = default) {
        // Clear any previous execution state
        _pausedTopics.Clear();

        var topic = _topicRegistry.GetTopic("ConversationStart");

        if (topic == null) {
            _logger.LogWarning("ConversationStartTopic not found in registry.");
            return;
        }

        if (topic is TopicFlow flow) {
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
        // Extract WaitForCompletion from the TriggerTopicActivity sender
        if (sender is not TriggerTopicActivity trigger) {
            _logger.LogWarning("[InsuranceAgentService] Topic triggered by unsupported sender type: {SenderType}", sender?.GetType().Name);
            return;
        }

        var waitForCompletion = trigger.WaitForCompletion;
        _logger.LogInformation("[InsuranceAgentService] Topic triggered: {TopicName} (WaitForCompletion: {WaitForCompletion})",
            e.TopicName, waitForCompletion);

        var nextTopic = _topicRegistry.GetTopic(e.TopicName);
        if (nextTopic is TopicFlow nextFlow) {

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
}
