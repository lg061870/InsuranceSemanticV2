using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using InsuranceAgent.Models;

public class InsuranceAgentService {
    private readonly TopicRegistry _topicRegistry;
    private readonly IConversationContext _context;
    private readonly TopicWorkflowContext _wfContext;
    private readonly ILogger<InsuranceAgentService> _logger;

    private ITopic? _activeTopic;
    private readonly Stack<TopicFlow> _pausedTopics = new Stack<TopicFlow>(); // Track paused topics

    // Outbound events → HybridChatService
    public event EventHandler<ActivityMessageEventArgs>? ActivityMessageReady;
    public event EventHandler<ActivityAdaptiveCardEventArgs>? ActivityAdaptiveCardReady;
    public event EventHandler<ActivityCompletedEventArgs>? ActivityCompleted;
    public event EventHandler<TopicLifecycleEventArgs>? TopicLifecycleChanged;
    public event EventHandler<MatchingTopicNotFoundEventArgs>? MatchingTopicNotFound;
    public event EventHandler<TopicInsertedEventArgs>? TopicInserted;
    public event EventHandler<ActivityLifecycleEventArgs>? ActivityLifecycleChanged;

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
                trigger.TopicTriggered += async (s, e) => {
                    _logger.LogInformation("[InsuranceAgentService] Topic triggered: {TopicName} (WaitForCompletion: {WaitForCompletion})", 
                        e.TopicName, trigger.WaitForCompletion);

                    var nextTopic = _topicRegistry.GetTopic(e.TopicName);
                    if (nextTopic is TopicFlow nextFlow) {
                        
                        if (trigger.WaitForCompletion) {
                            // NEW: Sub-topic pattern - pause current topic
                            if (_activeTopic is TopicFlow currentFlow) {
                                _logger.LogInformation("[InsuranceAgentService] Pausing topic '{CurrentTopic}' for sub-topic '{SubTopic}'",
                                    currentFlow.Name, nextFlow.Name);
                                _pausedTopics.Push(currentFlow);
                            }
                        }
                        
                        _activeTopic = nextFlow;
                        HookTopicEvents(nextFlow);

                        var result = await nextFlow.RunAsync();
                        
                        // NEW: Handle sub-topic completion and resumption
                        if (trigger.WaitForCompletion && result.IsCompleted) {
                            await HandleSubTopicCompletion(nextFlow, result);
                        }
                    }
                    else {
                        _logger.LogWarning("[InsuranceAgentService] Triggered topic {TopicName} not found or not a TopicFlow.", e.TopicName);
                    }
                };
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
            _activeTopic = flow;
            HookTopicEvents(flow);

            _logger.LogInformation("ConversationStartTopic activated");
            await flow.RunAsync(ct);
        }
        else {
            _logger.LogWarning("ConversationStartTopic is not a TopicFlow: {Type}", topic.GetType().Name);
        }
    }

    /// <summary>
    /// Handles the completion of a sub-topic and resumes the calling topic.
    /// </summary>
    private async Task HandleSubTopicCompletion(TopicFlow completedSubTopic, TopicResult subTopicResult)
    {
        if (_pausedTopics.Count == 0)
        {
            _logger.LogWarning("[InsuranceAgentService] Sub-topic '{SubTopic}' completed but no paused topics to resume",
                completedSubTopic.Name);
            return;
        }

        var callingTopic = _pausedTopics.Pop();
        _logger.LogInformation("[InsuranceAgentService] Sub-topic '{SubTopic}' completed, resuming '{CallingTopic}'",
            completedSubTopic.Name, callingTopic.Name);

        // Set completion data in context for the calling topic to access
        var completionData = new Dictionary<string, object>();
        if (subTopicResult.wfContext != null)
        {
            // Copy relevant data from the sub-topic's workflow context
            foreach (var key in subTopicResult.wfContext.GetKeys())
            {
                var value = subTopicResult.wfContext.GetValue<object>(key);
                if (value != null)
                {
                    completionData[key] = value;
                }
            }
        }
        
        _wfContext.SetValue("SubTopicCompletionData", completionData);
        
        // Pop the topic call from conversation context
        var callInfo = _context.PopTopicCall(completionData);
        if (callInfo != null)
        {
            _wfContext.SetValue("ResumeData", callInfo.ResumeData);
            _logger.LogInformation("[InsuranceAgentService] Call info retrieved: {CallingTopic} -> {SubTopic}",
                callInfo.CallingTopicName, callInfo.SubTopicName);
        }

        // Resume the calling topic
        _activeTopic = callingTopic;
        HookTopicEvents(callingTopic);
        
        try
        {
            // Continue execution from where it left off
            await callingTopic.StepAsync(null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[InsuranceAgentService] Error resuming topic '{TopicName}' after sub-topic completion",
                callingTopic.Name);
        }
    }
}
