using ConversaCore.Agentic;
using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Interfaces;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;

namespace InsuranceLeadsAgent.Services;

public class InsuranceLeadsAgent : DomainAgentService {
    public InsuranceLeadsAgent(
        TopicRegistry topicRegistry,
        IConversationContext context,
        TopicWorkflowContext wfContext,
        ILogger<DomainAgentService> logger)
        : base(topicRegistry, context, wfContext, logger) {
        // 🔥 IMPORTANT: take control of lifecycle
        this.TopicLifecycleChanged += OnTopicLifecycleChangedControlled;
    }

    protected override async Task StartConversationAsync(CancellationToken ct = default) {
        _pausedTopics.Clear();

        var topic = _topicRegistry.GetTopic("ConversationStart");
        if (topic is not TopicFlow flow) {
            _logger.LogWarning("ConversationStart not found or not TopicFlow.");
            return;
        }

        if (_activeTopic is TopicFlow current)
            UnhookTopicEvents(current);

        _activeTopic = flow;
        HookTopicEvents(flow);
        
        await flow.RunAsync(ct);
    }

    protected override async Task OnUserMessageReceivedAsync(string message, CancellationToken ct) {
        var safeMessage = message ?? string.Empty;
        _wfContext.SetValue("LastUserMessage", safeMessage);

        if (_activeTopic is TopicFlow flow &&
            flow.State == TopicFlow.FlowState.WaitingForInput) {
            await flow.ResumeAsync(safeMessage, ct);

            // 🔥 DO NOT auto-step blindly
            if (flow.State == TopicFlow.FlowState.Running) {
                await flow.StepAsync(null, ct);
            }

            return;
        }

        await base.OnUserMessageReceivedAsync(safeMessage, ct);
    }

    private async void OnTopicLifecycleChangedControlled(object? sender, TopicLifecycleEventArgs e) {
        if (e.State != TopicLifecycleState.Completed)
            return;

        // Only handle sub-topics (parent exists)
        if (_pausedTopics.Count == 0)
            return;

        var parent = _pausedTopics.Pop();

        if (parent is not TopicFlow parentFlow)
            return;

        _logger.LogInformation(
            "[InsuranceLeadsAgent] Controlled resume for parent topic '{Topic}'",
            parentFlow.Name);

        // Unhook current active topic (completed sub-topic)
        if (_activeTopic is TopicFlow current)
            UnhookTopicEvents(current);

        // Reactivate parent
        _activeTopic = parentFlow;
        HookTopicEvents(parentFlow);

        try {
            var currentActivity = parentFlow.GetCurrentActivity();

            // 🔥 SAFE RESUME:
            if (currentActivity != null &&
                parentFlow.State == TopicFlow.FlowState.WaitingForInput) {
                await parentFlow.ResumeAsync("Sub-topic completed", CancellationToken.None);
            }

            // 🔥 SAFE STEP:
            if (parentFlow.State == TopicFlow.FlowState.Running) {
                await parentFlow.StepAsync(null, CancellationToken.None);
            }
        } catch (Exception ex) {
            _logger.LogError(ex,
                "[InsuranceLeadsAgent] Controlled resume failed for parent '{Topic}'",
                parentFlow.Name);
        }
    }

    public void SubscribeToChatWindowEvents(ConversaCore.UI.Components.CustomChatWindowV3 chatWindow) {
        chatWindow.ConversationStartRequested += async (s, e) =>
            await OnConversationStartRequestedAsync(e.CancellationToken);

        chatWindow.UserMessageReceived += async (s, e) =>
            await OnUserMessageReceivedAsync(e.Message, e.CancellationToken);

        chatWindow.CardSubmitted += async (s, e) =>
            await OnCardSubmittedAsync(e.Data, e.CancellationToken);

        chatWindow.ConversationResetRequested += async (s, e) =>
            await OnConversationResetRequestedAsync(e.CancellationToken);
    }
}
