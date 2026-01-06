using ConversaCore.Cards;
using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Interfaces;
using ConversaCore.Models;
using ConversaCore.StateMachine;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Core.Interfaces;
using ConversaCore.Topics;
using InsuranceAgent.Cards;
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
    private const string ActivityId_ProcessComplianceData = "ProcessComplianceData";

    private ITopic? _activeTopic;
    private string? _activeCardId;
    private const string ActiveCardKey = "ActiveCardId";
    private readonly Stack<TopicFlow> _pausedTopics = new Stack<TopicFlow>(); // Track paused topics
    private readonly Dictionary<string, PendingSubTopic> _pendingSubTopics = new();

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
    public event EventHandler<AsyncQueryCompletedEventArgs>? AsyncActivityCompleted;
    public event EventHandler<CardStateChangedEventArgs>? CardStateChanged;

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

    #region Domain Agent Plumbing
    public async Task ProcessUserMessageAsync(string userMessage, CancellationToken ct = default) {
        var (topic, confidence) = await _topicRegistry.FindBestTopicAsync(userMessage, _context, ct);

        #region 🔧 Fallback topic setup
        var fallbackTopic = _topicRegistry.GetTopic("FallbackTopic") as TopicFlow;

        if (topic == null) {
            if (fallbackTopic == null) {
                LogWarn("0010001");
                MatchingTopicNotFound?.Invoke(this, new MatchingTopicNotFoundEventArgs(userMessage));
                return;
            }

            fallbackTopic.Context.SetValue("Fallback_UserPrompt", userMessage);
            LogInfo("0010002");

            if (_activeTopic is TopicFlow activeFlow &&
                activeFlow.GetCurrentActivity() is IPausableActivity waitingActivity &&
                waitingActivity.IsPaused) {
                var currentAct = waitingActivity as TopicFlowActivity;
                LogInfo("0010003");

                _pausedTopics.Push(activeFlow);
                UnhookTopicEvents(activeFlow);
            }

            _activeTopic = fallbackTopic;
            HookTopicEvents(fallbackTopic);

            await fallbackTopic.RunAsync(ct);
            return;
        }
        #endregion

        #region 🚀 Matched topic logic
        if (topic is not TopicFlow flow) {
            LogWarn("0010004", topic.GetType().Name);
            return;
        }

        if (_activeTopic is TopicFlow currentActiveTopic)
            UnhookTopicEvents(currentActiveTopic);

        _activeTopic = flow;
        HookTopicEvents(flow);

        LogInfo("0010005", flow.Name, confidence);
        await flow.RunAsync(ct);
        #endregion
    }

    private void AddDomainActivitiesToStartTopic(TopicFlow flow) {
        LogInfo("0010006");

        flow.Add(new GreetingActivity("Greet"));

        flow.Add(new TriggerTopicActivity(ActivityId_CollectCompliance, "ComplianceTopic", _logger, waitForCompletion: true));

        flow.Add(new SimpleActivity(ActivityId_ProcessComplianceData, (ctx, input) =>
        {
            _logger.LogInformation("[InsuranceAgentService] Processing compliance data before decision tree");
            return Task.FromResult<object?>(null);
        }));

        flow.AddRange(ComplianceFlowActivities() ?? new List<TopicFlowActivity>());

        LogInfo("0010007");
    }

    public async Task StartConversationAsync(CancellationToken ct = default) {
        _pausedTopics.Clear();

        var topic = _topicRegistry.GetTopic("ConversationStart");

        if (topic == null) {
            LogWarn("0010008");
            return;
        }

        if (topic is TopicFlow flow) {
            flow.RemoveActivity(ActivityId_CollectCompliance);
            flow.RemoveActivity("ProcessComplianceData");
            flow.RemoveActivity("TCPAConsentSwitch");

            AddDomainActivitiesToStartTopic(flow);

            if (_activeTopic is TopicFlow currentActiveTopic)
                UnhookTopicEvents(currentActiveTopic);

            _activeTopic = flow;
            HookTopicEvents(flow);

            LogInfo("0010009");
            await flow.RunAsync(ct);
        }
        else {
            LogWarn("0010004", topic.GetType().Name);
        }
    }

    public async Task ResetConversationAsync(CancellationToken ct = default) {
        LogInfo("0010010");

        _pausedTopics.Clear();
        _pendingSubTopics.Clear();

        if (_activeTopic is TopicFlow currentTopic) {
            UnhookTopicEvents(currentTopic);
            LogInfo("0010011", currentTopic.Name);
        }

        _activeTopic = null;

        _context.Reset();
        _wfContext.Clear();

        LogInfo("0010012");

        foreach (var topic in _topicRegistry.GetAllTopics()) {
            if (topic is TopicFlow tf) {
                LogInfo("0010013", tf.Name);
                tf.Reset();
            }
        }

        if (_topicRegistry.GetTopic("ConversationStart") is TopicFlow conversationStartTopic) {
            LogInfo("0010014");

            conversationStartTopic.Context.SetValue("ConversationStartTopic.HasRun", false);
            _context.SetValue("ConversationStartTopic.HasRun", false);

            _context.SetValue("Global_ConversationActive", false);
            _context.SetValue("Global_TopicHistory", new List<string>());
            _context.SetValue("Global_UserInteractionCount", 0);

            ForceTopicStateMachineToIdle(conversationStartTopic, "ConversationStartTopic");

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

                LogInfo("0010015");
            }
        }

        LogInfo("0010016");

        ConversationReset?.Invoke(this, new ConversationResetEventArgs());

        await Task.Delay(150, ct);

        LogInfo("0010017");
        await StartConversationAsync(ct);
    }

    private void ForceTopicStateMachineToIdle(TopicFlow topic, string name) {
        var fsmField = topic.GetType().BaseType?
            .GetField("_fsm", BindingFlags.NonPublic | BindingFlags.Instance);

        if (fsmField?.GetValue(topic) is ITopicStateMachine<TopicFlow.FlowState> fsm) {
            fsm.ForceState(TopicFlow.FlowState.Idle, $"Forced reset to Idle in {name}");
            fsm.ClearTransitionHistory();
            LogInfo("0010018", name);
        }
        else {
            LogWarn("0010019", name);
        }
    }

    private class PendingSubTopic {
        public TopicFlow CallingTopic { get; set; } = null!; 
        public TopicFlow SubTopic { get; set; } = null!; 
        public string CallingTopicName { get; set; } = string.Empty; 
        public string SubTopicName { get; set; } = string.Empty; 
        public DateTime StartTime { get; set; }
    }

    #endregion

    #region Insurance Domain Activities

    private TopicFlowActivity ToMarketingT1Topic(string id = "ToMarketingT1") {
        LogInfo("MT1_0001"); // ToMarketingT1Topic invoked

        return new TriggerTopicActivity(
            id,
            "MarketingT1Topic",
            _logger,
            waitForCompletion: false,
            conversationContext: _context
        );
    }

    private TopicFlowActivity ToMarketingT2Topic(string id = "ToMarketingT2") {
        LogInfo("MT2_0001"); // ToMarketingT2Topic invoked

        return new TriggerTopicActivity(
            id,
            "MarketingT2Topic",
            _logger,
            waitForCompletion: false,
            conversationContext: _context
        );
    }

    private TopicFlowActivity AskCaliforniaResidency(string id, TopicWorkflowContext context) {
        LogInfo("ACR_0001"); // Constructing card activity

        var cardActivity = new AdaptiveCardActivity<CaliforniaResidentCard, CaliforniaResidentModel>(
            id,
            context,
            cardFactory: card => {
                LogInfo("ACR_0002"); // Card factory invoked

                var result = card.Create(
                    isResident: true,
                    zip_code: context.GetValue<string>("zip_code"),
                    ccpa_acknowledgment: context.GetValue<string?>("ccpa_acknoledgement")
                );

                if (result is AdaptiveCardModel model) {
                    LogInfo("ACR_0003"); // Card created successfully
                }
                else {
                    LogWarn("ACR_0004"); // Unexpected type returned
                }

                return result;
            });

        LogInfo("ACR_0005"); // Returning AdaptiveCardActivity
        return cardActivity;
    }

    private List<TopicFlowActivity>? ComplianceFlowActivities() {

        return new List<TopicFlowActivity> {

        // -------------------------------------------------------
        // TCPA = YES
        // -------------------------------------------------------
        IfCase("TCPA_YES_BRANCH", ctx =>
            IsYes(ctx, "tcpa_consent"),

            ConditionalActivity<TopicFlowActivity>.If(
                "HAS_CA_INFO_YES_TCPA",
                c => IsYes(c, "is_california_resident"),

                // California Resident
                (id, c) => new CompositeActivity("ASK_CCPA_YES_CA", new List<TopicFlowActivity> {

                    AskCaliforniaResidency("CA_CARD_YES_CA", c),

                    ConditionalActivity<TopicFlowActivity>.If(
                        "HAS_CCPA_ACK",
                        cc => IsYes(cc, "ccpa_acknowledgment"),

                        (id2, cc) => ToMarketingT1Topic("AFTER_CCPA_YES"),
                        (id2, cc) => ToMarketingT2Topic("AFTER_CCPA_NO")
                    )
                }),

                // Not California
                (id, c) => ToMarketingT1Topic("NON_CA_TCPA_YES")
            )
        ),

        // -------------------------------------------------------
        // TCPA = NO
        // -------------------------------------------------------
        IfCase("TCPA_NO", ctx =>
            IsNo(ctx, "tcpa_consent"),
            new TriggerTopicActivity(
                "TO_MARKETING_T3_AFTER_TCPA_NO",
                "MarketingT3Topic",
                _logger,
                waitForCompletion: false,
                conversationContext: _context
            )
        )
    };
    }

    #endregion

    #region Event Triggers

    private void OnActivityCreated(object? sender, ActivityCreatedEventArgs e) {
        LogInfo("EVT_AC_0001"); // Activity created

        switch (e.Content) {
            default:
                LogTrace("EVT_AC_0002"); // Unhandled payload
                break;
        }
    }

    private async void OnTopicTriggered(object? sender, TopicTriggeredEventArgs e) {

        var senderType = sender?.GetType().Name ?? "Unknown";
        var senderId = sender is TopicFlowActivity activity
            ? activity.Id
            : (sender as TopicFlow)?.Name ?? "Unknown";

        LogInfo("EVT_TT_0001"); // TopicTriggered received

        // Console debug left intact
        Console.WriteLine($"[InsuranceAgentService.OnTopicTriggered] *** EVENT RECEIVED ***");
        Console.WriteLine($"TopicTriggered event received from sender '{senderId}' ({senderType}) for topic '{e.TopicName}'");
        Console.WriteLine($"OriginActivityId: {e.OriginActivityId ?? "(null)"}, BranchPath: {e.BranchPath ?? "(none)"}");
        Console.WriteLine($"[InsuranceAgentService.OnTopicTriggered] *** SENDER DETAILS *** Sender type: {sender?.GetType().FullName}, Is TopicFlow: {sender is TopicFlow}, Is Activity: {sender is TopicFlowActivity}");

        // Unsupported sender type
        if (sender is not ITopicTriggeredActivity trigger) {
            LogWarn("EVT_TT_0002"); // Unsupported sender
            return;
        }

        var waitForCompletion = trigger.WaitForCompletion;
        LogInfo("EVT_TT_0003"); // Trigger metadata

        var nextTopic = _topicRegistry.GetTopic(e.TopicName);

        if (nextTopic is TopicFlow nextFlow) {

            // Prevent duplicate activation
            if (_activeTopic != null && _activeTopic.Name == e.TopicName) {
                LogWarn("EVT_TT_0004");
                return;
            }

            // ==============================
            // WAIT-FOR-COMPLETION
            // ==============================
            if (waitForCompletion) {

                if (_activeTopic is TopicFlow currentFlow) {

                    LogInfo("EVT_TT_0005"); // Pausing parent

                    // Save active card
                    var parentCard = _context.GetValue<string>(ActiveCardKey);
                    if (!string.IsNullOrEmpty(parentCard)) {
                        _wfContext.SetValue($"{currentFlow.Name}_SavedCardId", parentCard);
                        LogInfo("EVT_TT_0006"); // Saved parent card
                    }

                    // Pause parent
                    _pausedTopics.Push(currentFlow);
                    UnhookTopicEvents(currentFlow);

                    // Hook sub-topic events
                    LogInfo("EVT_TT_0007");
                    HookTopicEvents(nextFlow);

                    // Register sub-topic
                    if (!_pendingSubTopics.ContainsKey(nextFlow.Name)) {
                        _pendingSubTopics[nextFlow.Name] = new PendingSubTopic {
                            SubTopic = nextFlow,
                            SubTopicName = nextFlow.Name,
                            CallingTopicName = currentFlow.Name,
                            StartTime = DateTime.UtcNow
                        };
                    }

                    LogInfo("EVT_TT_0008");
                }
            }
            else {
                // Legacy mode
                LogWarn("EVT_TT_0009");

                if (_activeTopic is TopicFlow currentFlow) {
                    if (_pausedTopics.Count > 0) {
                        LogInfo("EVT_TT_0010");
                        _pausedTopics.Clear();
                    }
                    UnhookTopicEvents(currentFlow);
                }

                HookTopicEvents(nextFlow);
            }

            // ========================================================
            // swap active topic
            // ========================================================
            if (_activeTopic is TopicFlow currentActiveTopic)
                UnhookTopicEvents(currentActiveTopic);

            _activeTopic = nextFlow;
            HookTopicEvents(nextFlow);

            // If WaitForCompletion, register it
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
                    LogInfo("EVT_TT_0011");
                }
                else {
                    LogWarn("EVT_TT_0012");
                }
            }

            // ========================================================
            // Run topic
            // ========================================================
            var result = await nextFlow.RunAsync();

            LogInfo("EVT_TT_0013");

            // Sub-topic completion handling
            if (!waitForCompletion) {
                LogInfo("EVT_TT_0014");

                if (result.IsCompleted && !string.IsNullOrEmpty(result.NextTopicName))
                    LogInfo("EVT_TT_0015");

            }
            else if (result.IsCompleted) {
                LogInfo("EVT_TT_0016");
                _pendingSubTopics.Remove(e.TopicName);
                await HandleSubTopicCompletion(nextFlow, result);
            }
            else {
                LogInfo("EVT_TT_0017");
            }
        }
        else {
            LogWarn("EVT_TT_0018"); // Topic not found
        }
    }

    private async void OnCustomEventTriggered(object? sender, CustomEventTriggeredEventArgs e) {
        LogInfo("EVT_CE_0001");
        _logger.LogWarning("[DEBUG] OnCustomEventTriggered called - EventName={EventName}, Sender={SenderType}, SubscriberCount={Count}",
            e.EventName, sender?.GetType().Name ?? "null", CustomEventTriggered?.GetInvocationList().Length ?? 0);

        CustomEventTriggered?.Invoke(this, e);

        if (e.WaitForResponse && sender is EventTriggerActivity triggerActivity) {
            await Task.Delay(100);

            var response = new {
                success = true,
                message = $"Event '{e.EventName}' processed successfully",
                timestamp = DateTime.UtcNow,
                processedBy = "InsuranceAgentService"
            };

            triggerActivity.HandleUIResponse(e.Context, response);
        }
    }

    private async void OnTopicLifecycleChanged(object? sender, TopicLifecycleEventArgs e) {
        LogInfo("EVT_LF_0001");

        try {

            // Sub-topic completion via lifecycle
            if (e.State == TopicLifecycleState.Completed &&
                _pendingSubTopics.ContainsKey(e.TopicName)) {

                if (_pausedTopics.Count == 0) {
                    LogTrace("EVT_LF_0002");
                    return;
                }

                var pending = _pendingSubTopics[e.TopicName];
                _pendingSubTopics.Remove(e.TopicName);

                LogInfo("EVT_LF_0003");

                var result = TopicResult.CreateCompleted("Sub-topic completed", pending.SubTopic.Context);
                await HandleSubTopicCompletion(pending.SubTopic, result);
                return;
            }

            // Fallback topic completion
            if (e.State == TopicLifecycleState.Completed && e.TopicName == "FallbackTopic") {
                LogInfo("EVT_LF_0004");

                var fallback = _topicRegistry.GetTopic("FallbackTopic") as TopicFlow;
                if (fallback == null) {
                    LogWarn("EVT_LF_0005");
                    return;
                }

                var fallbackResult = TopicResult.CreateCompleted("Fallback completed", fallback.Context);
                await HandleFallbackCompletion(fallback, fallbackResult);
                return;
            }

            LogTrace("EVT_LF_0006");

        } catch (Exception ex) {
            LogError("EVT_LF_0007");
        }
    }

    private void OnCardStateChanged(string cardId, CardState newState) {
        LogInfo("EVT_CS_0001");
        CardStateChanged?.Invoke(this, new CardStateChangedEventArgs(cardId, newState));
    }

    private void OnMessageReady(string content) {
        ActivityMessageReady?.Invoke(this,
            new ActivityMessageEventArgs(new ChatMessage {
                Content = content,
                IsFromUser = false,
                Timestamp = DateTime.Now
            }));
    }

    #endregion

    #region Event Handlers

    public async Task HandleCardSubmitAsync(Dictionary<string, object> data, CancellationToken ct) {
        if (_activeTopic is TopicFlow flow) {
            var current = flow.GetCurrentActivity();

            if (current is IAdaptiveCardActivity cardAct) {

                LogInfo("EVT_HC_0001"); // Deliver card input
                cardAct.OnInputCollected(new AdaptiveCardInputCollectedEventArgs(data));

                await flow.StepAsync(null, ct);
            }
            else {
                LogWarn("EVT_HC_0002"); // Not adaptive card activity
                OnMessageReady("⚠️ This step cannot accept card input.");
            }
        }
        else {
            OnMessageReady("⚠️ Unable to resume workflow (no active topic).");
        }
    }
    private async Task HandleFallbackCompletion(TopicFlow fallbackTopic, TopicResult result) {
        if (_pausedTopics.Count == 0) {
            LogInfo("EVT_FB_0001");
            return;
        }

        var callingTopic = _pausedTopics.Pop();
        LogInfo("EVT_FB_0002");

        UnhookTopicEvents(fallbackTopic);
        _activeTopic = callingTopic;
        HookTopicEvents(callingTopic);

        var previousCard = _context.GetValue<string>(ActiveCardKey);
        if (!string.IsNullOrEmpty(previousCard)) {
            LogInfo("EVT_FB_0003");
            OnCardStateChanged(previousCard, CardState.Active);
        }

        try {
            var current = callingTopic.GetCurrentActivity() as IPausableActivity;
            if (current != null) {
                await current.ResumeAsync("FallbackTopic completed", CancellationToken.None);
            }
            else {
                LogInfo("EVT_FB_0004");
                await callingTopic.ResumeAsync("FallbackTopic completed", CancellationToken.None);
            }
        } catch (Exception ex) {
            LogError("EVT_FB_0004", ex);
        }
    }
    private void HandleAsyncActivityCompleted(object? sender, AsyncQueryCompletedEventArgs e) {

        LogInfo("EVT_AS_0001");

        if (e.Activity == null) {
            LogInfo("EVT_AS_0002");
            AsyncActivityCompleted?.Invoke(this, e);
            return;
        }

        var followup = e.Activity;

        if (_activeTopic is not TopicFlow flow) {
            LogWarn("EVT_AS_0003");
            AsyncActivityCompleted?.Invoke(this, e);
            return;
        }

        LogInfo("EVT_AS_0004");

        HookActivityEvents(followup);
        flow.InsertNext(followup);

        AsyncActivityCompleted?.Invoke(this, e);
    }
    private async void HandleTopicLifecycleChanged(object? sender, TopicLifecycleEventArgs e) {

        LogInfo("EVT_TH_0001");

        TopicLifecycleChanged?.Invoke(this, e);

        if (e.State == TopicLifecycleState.Completed) {

            if (_pausedTopics.Count == 0) {
                LogTrace("EVT_TH_0002");
                return;
            }

            try {
                var parentTopic = _pausedTopics.Pop();

                LogInfo("EVT_TH_0003");

                var previousTopic = _activeTopic;
                _activeTopic = parentTopic;

                LogInfo("EVT_TH_0004");

                HookTopicEvents(parentTopic);

                var savedCardId = _context.GetValue<string>($"{parentTopic.Name}_SavedCardId");
                if (!string.IsNullOrEmpty(savedCardId)) {
                    LogInfo("EVT_TH_0005");
                    _context.SetValue(ActiveCardKey, savedCardId);
                    OnCardStateChanged(savedCardId, CardState.Active);
                }
                else {
                    LogInfo("EVT_TH_0006");
                }

                if (parentTopic is TopicFlow parentFlow) {
                    LogTrace("EVT_TH_0007");
                }

                LogInfo("EVT_TH_0008");
                await parentTopic.RunAsync();

                LogInfo("EVT_TH_0009");
            } catch (Exception ex) {
                LogError("EVT_TH_0010", ex);
            }
        }
    }
    private void HandleActivityCompleted(object? sender, ActivityCompletedEventArgs e) {
        LogInfo("EVT_ACM_0001");
        ActivityCompleted?.Invoke(this, e);
    }
    private void HandleTopicInserted(object? sender, TopicInsertedEventArgs e)
        => TopicInserted?.Invoke(this, e);
    private void HandleActivityLifecycleChanged(object? sender, ActivityLifecycleEventArgs e) {
        LogInfo("EVT_AL_0001");
        ActivityLifecycleChanged?.Invoke(this, e);
    }
    private void HandleMessageEmitted(object? sender, MessageEmittedEventArgs e) {
        LogInfo("EVT_ME_0001");
        ActivityMessageReady?.Invoke(this,
            new ActivityMessageEventArgs(
                new ChatMessage {
                    Content = e.Message,
                    IsFromUser = false,
                    Timestamp = DateTime.Now
                }));
    }
    private void HandleCardJsonSent(object? sender, CardJsonEventArgs e) {

        LogInfo("EVT_CJS_0001");

        try {
            if (_pausedTopics.Count > 0) {
                var parentTopic = _pausedTopics.Peek();
                var currentActiveCard = _context.GetValue<string>(ActiveCardKey);

                if (!string.IsNullOrEmpty(currentActiveCard) && currentActiveCard != e.CardId) {
                    _context.SetValue($"{parentTopic.Name}_SavedCardId", currentActiveCard);
                    LogInfo("EVT_CJS_0002");
                }
            }
        } catch (Exception ex) {
            LogError("EVT_CJS_0003", ex);
        }

        var previousCard = _context.GetValue<string>(ActiveCardKey);
        if (!string.IsNullOrEmpty(previousCard) && previousCard != e.CardId) {
            LogInfo("EVT_CJS_0004");
            OnCardStateChanged(previousCard, CardState.ReadOnly);
        }

        LogInfo("EVT_CJS_0005");
        _context.SetValue(ActiveCardKey, e.CardId);
        _context.SetValue($"{e.CardId}_IsRequired", e.IsRequired);

        OnCardStateChanged(e.CardId, CardState.Active);

        LogInfo("EVT_CJS_0006");
        ActivityAdaptiveCardReady?.Invoke(this,
            new ActivityAdaptiveCardEventArgs(
                e.CardJson ?? "{}",
                e.CardId,
                e.RenderMode,
                e.IsRequired));

        try {
            if (e.IsRequired) {
                LogInfo("EVT_CJS_0007");
                PromptInputStateChanged?.Invoke(this, new PromptInputStateChangedEventArgs(false, e.CardId));
            }
            else {
                LogInfo("EVT_CJS_0008");
                PromptInputStateChanged?.Invoke(this, new PromptInputStateChangedEventArgs(true, e.CardId));
            }
        } catch (Exception ex) {
            LogError("EVT_CJS_0009", ex);
        }
    }
    private void HandleValidationFailed(object? sender, ValidationFailedEventArgs e) {
        LogWarn("EVT_VF_0001");

        if (!string.IsNullOrEmpty(_activeCardId))
            OnCardStateChanged(_activeCardId, CardState.Active);
    }
    private async Task HandleSubTopicCompletion(TopicFlow completedSubTopic, TopicResult subTopicResult) {
        if (_pausedTopics.Count == 0) {
            LogWarn("EVT_ST_0001");
            return;
        }

        var callingTopic = _pausedTopics.Pop();
        LogInfo("EVT_ST_0002");

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
            LogInfo("EVT_ST_0003");
            _wfContext.SetValue("ResumeData", callInfo.ResumeData);
        }

        if (_activeTopic is TopicFlow currentActiveTopic)
            UnhookTopicEvents(currentActiveTopic);

        _activeTopic = callingTopic;
        HookTopicEvents(callingTopic);

        var previousCard = _context.GetValue<string>(ActiveCardKey);
        if (!string.IsNullOrEmpty(previousCard)) {
            LogInfo("EVT_ST_0004");
            OnCardStateChanged(previousCard, CardState.Active);
        }

        try {
            await callingTopic.ResumeAsync("Sub-topic completed", CancellationToken.None);
        } catch (Exception ex) {
            LogError("EVT_ST_0005", ex);
        }
    }
    private void HandleCardJsonEmitted(object? s, EventArgs e) => LogTrace("EVT_DBG_0001");
    private void HandleCardJsonSending(object? s, EventArgs e) => LogTrace("EVT_DBG_0002");
    private void HandleCardJsonRendered(object? s, EventArgs e) => LogTrace("EVT_DBG_0003");
    private void HandleCardDataReceived(object? s, EventArgs e) => LogTrace("EVT_DBG_0004");
    private void HandleModelBound(object? s, EventArgs e) => LogTrace("EVT_DBG_0005");

    #endregion

    #region Event Wiring
    private void HookTopicEvents(TopicFlow flow) {
        // ===== TOPIC-LEVEL EVENTS =====
        flow.TopicLifecycleChanged += HandleTopicLifecycleChanged;
        flow.TopicLifecycleChanged += OnTopicLifecycleChanged; // completion tracking
        flow.ActivityCreated += OnActivityCreated;
        flow.ActivityCompleted += HandleActivityCompleted;
        flow.TopicInserted += HandleTopicInserted;

        // 🔥 IMPORTANT: async-completion (correct place)
        flow.AsyncActivityCompleted += HandleAsyncActivityCompleted;

        // ===== TOPIC-LEVEL TRIGGERS =====
        if (flow is ITopicTriggeredActivity topicTrigger) {
            Console.WriteLine($"[InsuranceAgentService.HookTopicEvents] Subscribing to TopicTriggered for topic '{flow.Name}'");
            topicTrigger.TopicTriggered += OnTopicTriggered;
        }

        // ===== ACTIVITY-LEVEL EVENTS =====
        foreach (var act in flow.GetAllActivities()) {
            HookActivityEvents(act);
        }

        _logger.LogInformation("[InsuranceAgentService] ✔ Hooked topic '{Topic}' and all activities", flow.Name);
    }
    private void UnhookTopicEvents(TopicFlow flow) {
        if (flow == null)
            return;

        _logger.LogInformation("[InsuranceAgentService] Unhooking events for topic flow: {FlowName}", flow.Name);

        // --- 🔹 Deactivate any card still active ---
        var activeCard = _context.GetValue<string>(ActiveCardKey);
        if (!string.IsNullOrEmpty(activeCard)) {
            _logger.LogInformation("[InsuranceAgentService] Deactivating lingering active card {CardId} before unhook", activeCard);
            OnCardStateChanged(activeCard, CardState.ReadOnly);
            _wfContext.RemoveValue(ActiveCardKey);
        }

        // ===== TOPIC-LEVEL EVENTS =====
        flow.TopicLifecycleChanged -= HandleTopicLifecycleChanged;
        flow.TopicLifecycleChanged -= OnTopicLifecycleChanged;
        flow.ActivityCreated -= OnActivityCreated;
        flow.ActivityCompleted -= HandleActivityCompleted;
        flow.TopicInserted -= HandleTopicInserted;

        // 🔥 IMPORTANT: async-completion (must match Hook)
        flow.AsyncActivityCompleted -= HandleAsyncActivityCompleted;

        // ===== TOPIC-LEVEL TRIGGERS =====
        if (flow is ITopicTriggeredActivity topicTrigger)
            topicTrigger.TopicTriggered -= OnTopicTriggered;

        // ===== ACTIVITY-LEVEL EVENTS =====
        foreach (var act in flow.GetAllActivities()) {
            UnhookActivityEvents(act);
        }

        _activeCardId = null;

        _logger.LogInformation("[InsuranceAgentService] ✅ Unhooked topic and activity events for {FlowName}", flow.Name);
    }
    private void HookActivityEvents(TopicFlowActivity child) {
        // ✅ Defensive: Unhook first to prevent duplicate subscriptions
        UnhookActivityEvents(child);

        // --- Lifecycle ---
        child.ActivityLifecycleChanged += HandleActivityLifecycleChanged;
        child.MessageEmitted += HandleMessageEmitted;

        // --- Adaptive cards ---
        if (child is IAdaptiveCardActivity cardAct) {
            cardAct.CardJsonSent += HandleCardJsonSent;
            cardAct.ValidationFailed += HandleValidationFailed;
            cardAct.CardJsonEmitted += HandleCardJsonEmitted;
            cardAct.CardJsonSending += HandleCardJsonSending;
            cardAct.CardJsonRendered += HandleCardJsonRendered;
            cardAct.CardDataReceived += HandleCardDataReceived;
            cardAct.ModelBound += HandleModelBound;
        }

        // --- Topic-triggered events ---
        if (child is ITopicTriggeredActivity topicTrigger)
            topicTrigger.TopicTriggered += OnTopicTriggered;

        // --- Custom event-triggered events ---
        if (child is ICustomEventTriggeredActivity customTrigger) {
            _logger.LogWarning("[DEBUG] Hooking CustomEventTriggered for activity '{ActivityId}' (Type: {Type})",
                child.Id, child.GetType().Name);
            customTrigger.CustomEventTriggered += OnCustomEventTriggered;
        }

        // --- Conditional containers ---
        if (child is ConditionalActivity<TriggerTopicActivity> conditional) {
            conditional.TopicTriggered += OnTopicTriggered;
            conditional.CustomEventTriggered += OnCustomEventTriggered;
        }

        // --- Composite containers ---
        if (child is CompositeActivity composite) {
            composite.TopicTriggered += OnTopicTriggered;
            composite.CustomEventTriggered += OnCustomEventTriggered;
        }

        _logger.LogInformation(
            "[InsuranceAgentService] ✔ Hooked runtime activity '{Id}' ({Type})",
            child.Id, child.GetType().Name);
    }
    private void UnhookActivityEvents(TopicFlowActivity child) {
        // --- Lifecycle ---
        child.ActivityLifecycleChanged -= HandleActivityLifecycleChanged;
        child.MessageEmitted -= HandleMessageEmitted;

        // --- Adaptive cards ---
        if (child is IAdaptiveCardActivity cardAct) {
            cardAct.CardJsonSent -= HandleCardJsonSent;
            cardAct.ValidationFailed -= HandleValidationFailed;
            cardAct.CardJsonEmitted -= HandleCardJsonEmitted;
            cardAct.CardJsonSending -= HandleCardJsonSending;
            cardAct.CardJsonRendered -= HandleCardJsonRendered;
            cardAct.CardDataReceived -= HandleCardDataReceived;
            cardAct.ModelBound -= HandleModelBound;
        }

        // --- Topic-triggered events ---
        if (child is ITopicTriggeredActivity topicTrigger)
            topicTrigger.TopicTriggered -= OnTopicTriggered;

        // --- Custom-triggered events ---
        if (child is ICustomEventTriggeredActivity customTrigger)
            customTrigger.CustomEventTriggered -= OnCustomEventTriggered;

        // --- Conditional containers ---
        if (child is ConditionalActivity<TriggerTopicActivity> conditional) {
            conditional.TopicTriggered -= OnTopicTriggered;
            conditional.CustomEventTriggered -= OnCustomEventTriggered;
        }

        // --- Composite containers ---
        if (child is CompositeActivity composite) {
            composite.TopicTriggered -= OnTopicTriggered;
            composite.CustomEventTriggered -= OnCustomEventTriggered;
        }

        _logger.LogInformation(
            "[InsuranceAgentService] ✔ Unhooked runtime activity '{Id}' ({Type})",
            child.Id, child.GetType().Name);
    }
    #endregion

    #region Conditional Activity Helpers
    private TopicFlowActivity IfCase(string id, Func<TopicWorkflowContext, bool> condition, TopicFlowActivity activity) {
        return ConditionalActivity<TopicFlowActivity>.If(
            id,
            condition,
            (yesId, ctx) => activity,
            (noId, ctx) => Skip(id));
    }
    private static TopicFlowActivity Skip(string id)
        => new SimpleActivity($"{id}_SKIP", (c, d) => Task.FromResult<object?>(null));
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
    #endregion

    #region Logging Helpers
    private string ResolveLogMessage(string codeOrMessage, params object[] args) {
        if (_logMessages.TryGetValue(codeOrMessage, out var template))
            return string.Format(template, args);

        return string.Format(codeOrMessage, args);
    }
    private void LogInfo(string codeOrMessage, params object[] args)
        => _logger.LogInformation(ResolveLogMessage(codeOrMessage, args));
    private void LogWarn(string codeOrMessage, params object[] args)
        => _logger.LogWarning(ResolveLogMessage(codeOrMessage, args));
    private void LogError(string codeOrMessage, params object[] args)
        => _logger.LogError(ResolveLogMessage(codeOrMessage, args));
    private void LogTrace(string codeOrMessage, params object[] args) {
#if DEBUG
        _logger.LogDebug(ResolveLogMessage(codeOrMessage, args));
#endif
    }

    private readonly Dictionary<string, string> _logMessages = new()
{
    { "0010001", "[InsuranceAgentService] FallbackTopic not found in registry." },
    { "0010002", "[InsuranceAgentService] Injected Fallback_UserPrompt into FallbackTopic." },
    { "0010003", "[InsuranceAgentService] Detected paused activity. Routing message to FallbackTopic." },
    { "0010004", "Resolved topic is not a TopicFlow." },
    { "0010005", "Activated topic {0} (confidence {1:P2})" },
    { "0010006", "[InsuranceAgentService] Adding domain-specific activities to ConversationStartTopic" },
    { "0010007", "[InsuranceAgentService] Domain-specific activities added to ConversationStartTopic" },
    { "0010008", "ConversationStartTopic not found in registry." },
    { "0010009", "ConversationStartTopic activated" },
    { "0010010", "[InsuranceAgentService] Resetting conversation..." },
    { "0010011", "[InsuranceAgentService] Unhooked topic events from {0}" },
    { "0010012", "[InsuranceAgentService] all context entries were purged" },
    { "0010013", "[InsuranceAgentService] Resetting topic: {0}" },
    { "0010014", "[InsuranceAgentService] Resetting ConversationStartTopic runtime flags" },
    { "0010015", "[InsuranceAgentService] ComplianceTopic data and flags cleared" },
    { "0010016", "[InsuranceAgentService] ✅ All topics and contexts fully reset" },
    { "0010017", "[InsuranceAgentService] Restarting conversation after reset..." },
    { "0010018", "[InsuranceAgentService] {0} FSM forced to Idle" },
    { "0010019", "[InsuranceAgentService] Could not access FSM for {0}" },
    { "MT1_0001", "[DEBUG] ToMarketingT1Topic invoked → triggering T1" },
    { "MT2_0001", "[DEBUG] ToMarketingT2Topic invoked → triggering T2" },
    { "ACR_0001", "[AskCaliforniaResidency] Constructing card activity" },
    { "ACR_0002", "[AskCaliforniaResidency] Card factory invoked" },
    { "ACR_0003", "[AskCaliforniaResidency] Card created OK" },
    { "ACR_0004", "[AskCaliforniaResidency] Unexpected card return type" },
    { "ACR_0005", "[AskCaliforniaResidency] Returning AdaptiveCardActivity" },
        // ============================================================
    // ACTIVITY CREATED
    // ============================================================
    { "EVT_AC_0001", "[InsuranceAgentService] ActivityCreated" },
    { "EVT_AC_0002", "[InsuranceAgentService] Unhandled activity payload type" },

    // ============================================================
    // TOPIC TRIGGERED
    // ============================================================
    { "EVT_TT_0001", "[InsuranceAgentService] TopicTriggered received" },
    { "EVT_TT_0002", "[InsuranceAgentService] Topic triggered by unsupported sender type" },
    { "EVT_TT_0003", "[InsuranceAgentService] TopicTriggered metadata processed" },
    { "EVT_TT_0004", "[InsuranceAgentService] Duplicate topic trigger ignored (topic already active)" },
    { "EVT_TT_0005", "[InsuranceAgentService] Pausing parent topic for subtopic execution" },
    { "EVT_TT_0006", "[InsuranceAgentService] Saved parent active card before entering sub-topic" },
    { "EVT_TT_0007", "[InsuranceAgentService] Hooking sub-topic events" },
    { "EVT_TT_0008", "[InsuranceAgentService] Sub-topic registered for completion tracking" },
    { "EVT_TT_0009", "[InsuranceAgentService] Legacy non-blocking topic handoff" },
    { "EVT_TT_0010", "[InsuranceAgentService] Clearing paused topics prior to legacy handoff" },
    { "EVT_TT_0011", "[InsuranceAgentService] Sub-topic registered (WaitForCompletion=true)" },
    { "EVT_TT_0012", "[InsuranceAgentService] WaitForCompletion=true but no parent topic found on stack" },
    { "EVT_TT_0013", "[InsuranceAgentService] Topic execution completed" },
    { "EVT_TT_0014", "[InsuranceAgentService] Legacy topic handoff completed" },
    { "EVT_TT_0015", "[InsuranceAgentService] Legacy topic provided next topic to execute" },
    { "EVT_TT_0016", "[InsuranceAgentService] Sub-topic finished immediately; resuming parent topic" },
    { "EVT_TT_0017", "[InsuranceAgentService] Sub-topic started; waiting for completion" },
    { "EVT_TT_0018", "[InsuranceAgentService] Topic triggered but not found in registry" },

    // ============================================================
    // CUSTOM EVENT TRIGGERED
    // ============================================================
    { "EVT_CE_0001", "[InsuranceAgentService] Custom event triggered" },

    // ============================================================
    // TOPIC LIFECYCLE
    // ============================================================
    { "EVT_LF_0001", "[InsuranceAgentService] TopicLifecycleChanged event received" },
    { "EVT_LF_0002", "[InsuranceAgentService] Duplicate lifecycle completion ignored (parent already resumed)" },
    { "EVT_LF_0003", "[InsuranceAgentService] Sub-topic completed via lifecycle; resuming parent" },
    { "EVT_LF_0004", "[InsuranceAgentService] FallbackTopic completed — attempting to resume active topic" },
    { "EVT_LF_0005", "[InsuranceAgentService] FallbackTopic not found in registry during lifecycle event" },
    { "EVT_LF_0006", "[InsuranceAgentService] Topic lifecycle transition ignored (not actionable)" },
    { "EVT_LF_0007", "[InsuranceAgentService] Error processing TopicLifecycleChanged" },

    // ============================================================
    // CARD EVENTS
    // ============================================================
    { "EVT_CS_0001", "[InsuranceAgentService] Card state changed" },
    
    // ============================================================
    // CARD SUBMIT HANDLER
    // ============================================================
    { "EVT_HC_0001", "[InsuranceAgentService] Delivering card input to activity" },
    { "EVT_HC_0002", "[InsuranceAgentService] Current activity is not adaptive-card-capable" },

    // ============================================================
    // FALLBACK COMPLETION HANDLER
    // ============================================================
    { "EVT_FB_0001", "[InsuranceAgentService] FallbackTopic completed but no paused topics to resume." },
    { "EVT_FB_0002", "[InsuranceAgentService] Resuming paused topic after fallback answer." },
    { "EVT_FB_0003", "[InsuranceAgentService] Reactivating previously active card after fallback completion" },
    { "EVT_FB_0004", "[InsuranceAgentService] No pausable activity found; resuming topic normally." },

    // ============================================================
    // ASYNC ACTIVITY COMPLETED
    // ============================================================
    { "EVT_AS_0001", "[InsuranceAgentService] AsyncActivityCompleted received" },
    { "EVT_AS_0002", "[InsuranceAgentService] AsyncActivityCompleted: No follow-up activity returned" },
    { "EVT_AS_0003", "[InsuranceAgentService] Cannot InsertNext for async follow-up — no active TopicFlow." },
    { "EVT_AS_0004", "[InsuranceAgentService] Inserting async follow-up activity into topic" },

    // ============================================================
    // TOPIC LIFECYCLE (LOCAL HANDLER VARIATION)
    // ============================================================
    { "EVT_TH_0001", "[InsuranceAgentService] TopicLifecycleChanged received" },
    { "EVT_TH_0002", "[InsuranceAgentService] Topic completed but no paused topics remain." },
    { "EVT_TH_0003", "[InsuranceAgentService] Resuming parent topic after sub-topic completed." },
    { "EVT_TH_0004", "[InsuranceAgentService] Active topic updated" },
    { "EVT_TH_0005", "[InsuranceAgentService] Restoring saved parent card after sub-topic completion." },
    { "EVT_TH_0006", "[InsuranceAgentService] No saved card state found for parent topic." },
    { "EVT_TH_0007", "[InsuranceAgentService] Parent flow state restored." },
    { "EVT_TH_0008", "[InsuranceAgentService] Parent topic resumed." },
    { "EVT_TH_0009", "[InsuranceAgentService] Resume sequence completed for parent topic." },
    { "EVT_TH_0010", "[InsuranceAgentService] Error while resuming paused topic after sub-topic completed." },

    // ============================================================
    // ACTIVITY COMPLETED
    // ============================================================
    { "EVT_ACM_0001", "[InsuranceAgentService] ActivityCompleted" },

    // ============================================================
    // ACTIVITY LIFECYCLE
    // ============================================================
    { "EVT_AL_0001", "[InsuranceAgentService] ActivityLifecycleChanged" },

    // ============================================================
    // MESSAGE EMITTED
    // ============================================================
    { "EVT_ME_0001", "[InsuranceAgentService] MessageEmitted" },

    // ============================================================
    // CARD JSON SENT
    // ============================================================
    { "EVT_CJS_0001", "[InsuranceAgentService] CardJsonSent" },
    { "EVT_CJS_0002", "[InsuranceAgentService] Preserved parent card under paused topic" },
    { "EVT_CJS_0003", "[InsuranceAgentService] Error preserving parent card before new card activation" },
    { "EVT_CJS_0004", "[InsuranceAgentService] Deactivating previous card" },
    { "EVT_CJS_0005", "[InsuranceAgentService] Activating new card" },
    { "EVT_CJS_0006", "[InsuranceAgentService] Preparing to trigger ActivityAdaptiveCardReady" },
    { "EVT_CJS_0007", "[InsuranceAgentService] Disabling prompt for required card" },
    { "EVT_CJS_0008", "[InsuranceAgentService] Enabling prompt (card not required)" },
    { "EVT_CJS_0009", "[InsuranceAgentService] Error toggling prompt state for required/non-required card" },

    // ============================================================
    // VALIDATION FAILED
    // ============================================================
    { "EVT_VF_0001", "[InsuranceAgentService] ValidationFailed" },

    // ============================================================
    // SUBTOPIC COMPLETION
    // ============================================================
    { "EVT_ST_0001", "[InsuranceAgentService] Sub-topic completed but no paused topics to resume" },
    { "EVT_ST_0002", "[InsuranceAgentService] Sub-topic completed, resuming calling topic" },
    { "EVT_ST_0003", "[InsuranceAgentService] Call info retrieved (CallingTopic -> SubTopic)" },
    { "EVT_ST_0004", "[InsuranceAgentService] Reactivating previous card after sub-topic completion" },
    { "EVT_ST_0005", "[InsuranceAgentService] Error resuming topic after sub-topic completion" },

    // ============================================================
    // INTERNAL DEBUG EVENTS
    // ============================================================
    { "EVT_DBG_0001", "CardJsonEmitted (internal)" },
    { "EVT_DBG_0002", "CardJsonSending (internal)" },
    { "EVT_DBG_0003", "CardJsonRendered (client ack)" },
    { "EVT_DBG_0004", "CardDataReceived (internal)" },
    { "EVT_DBG_0005", "ModelBound (internal)" }


};

    #endregion
}

