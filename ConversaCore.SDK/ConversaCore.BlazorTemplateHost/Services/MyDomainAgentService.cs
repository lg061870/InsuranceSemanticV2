using ConversaCore.Agentic;
using ConversaCore.Interfaces;
using ConversaCore.Services;
using ConversaCore.Topics;
using ConversaCore.Context;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;

namespace ConversaCore.BlazorTemplateHost.Services;

/// <summary>
/// Minimal domain-specific agent for the ConversaCore Blazor template.
/// Hosts the TopicFlow runtime and bridges UI events to ConversaCore.
/// </summary>
public class MyDomainAgentService : DomainAgentService
{
    public MyDomainAgentService(
        TopicRegistry topicRegistry,
        IConversationContext context,
        TopicWorkflowContext wfContext,
        ILogger<DomainAgentService> logger)
        : base(topicRegistry, context, wfContext, logger)
    {
    }

    /// <summary>
    /// Starts the conversation by running the ConversationStart topic if it exists.
    /// This satisfies the DomainAgentService requirement to override StartConversationAsync
    /// and prevents the default NotImplementedException.
    /// </summary>
    protected override async Task StartConversationAsync(CancellationToken ct = default)
    {
        _pausedTopics.Clear();

        var topic = _topicRegistry.GetTopic("ConversationStart");
        if (topic == null)
        {
            _logger.LogWarning("[MyDomainAgentService] ConversationStart topic not found; no startup flow will run.");
            return;
        }

        if (topic is ConversaCore.TopicFlow.TopicFlow flow)
        {
            if (_activeTopic is ConversaCore.TopicFlow.TopicFlow currentActive)
            {
                UnhookTopicEvents(currentActive);
            }

            _activeTopic = flow;
            HookTopicEvents(flow);

            _logger.LogInformation("[MyDomainAgentService] Starting ConversationStart topic.");
            await flow.RunAsync(ct);
        }
        else
        {
            _logger.LogWarning(
                "[MyDomainAgentService] ConversationStart topic type {Type} is not a TopicFlow.",
                topic.GetType().Name);
        }
    }

    /// <summary>
    /// Example override that captures the last raw user message into workflow context
    /// and resumes any active TopicFlow that is waiting for input.
    /// </summary>
    protected override async Task OnUserMessageReceivedAsync(string message, CancellationToken ct)
    {
        var safeMessage = message ?? string.Empty;
        _wfContext.SetValue("LastUserMessage", safeMessage);

        if (_activeTopic is ConversaCore.TopicFlow.TopicFlow flow &&
            flow.State == ConversaCore.TopicFlow.TopicFlow.FlowState.WaitingForInput)
        {
            await flow.ResumeAsync(safeMessage, ct);

            if (flow.State != ConversaCore.TopicFlow.TopicFlow.FlowState.WaitingForInput)
            {
                await flow.StepAsync(null, ct);
            }

            return;
        }

        await base.OnUserMessageReceivedAsync(safeMessage, ct);
    }

    /// <summary>
    /// Wire up UI events from CustomChatWindowV3 to this agent service.
    /// </summary>
    public void SubscribeToChatWindowEvents(ConversaCore.UI.Components.CustomChatWindowV3 chatWindow)
    {
        _logger.LogInformation("[MyDomainAgentService] Subscribing to CustomChatWindowV3 events");

        chatWindow.ConversationStartRequested += async (s, e) =>
        {
            _logger.LogInformation("[MyDomainAgentService] Event: ConversationStartRequested");
            await OnConversationStartRequestedAsync(e.CancellationToken);
        };

        chatWindow.UserMessageReceived += async (s, e) =>
        {
            _logger.LogInformation("[MyDomainAgentService] Event: UserMessageReceived");
            await OnUserMessageReceivedAsync(e.Message, e.CancellationToken);
        };

        chatWindow.CardSubmitted += async (s, e) =>
        {
            _logger.LogInformation("[MyDomainAgentService] Event: CardSubmitted");
            await OnCardSubmittedAsync(e.Data, e.CancellationToken);
        };

        chatWindow.ConversationResetRequested += async (s, e) =>
        {
            _logger.LogInformation("[MyDomainAgentService] Event: ConversationResetRequested");
            await OnConversationResetRequestedAsync(e.CancellationToken);
        };

        _logger.LogInformation("[MyDomainAgentService] Event subscriptions complete");
    }
}
