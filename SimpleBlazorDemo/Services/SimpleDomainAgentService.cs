using ConversaCore.Agentic;
using ConversaCore.Interfaces;
using ConversaCore.Services;
using ConversaCore.Topics;
using ConversaCore.Context;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging;

namespace SimpleBlazorDemo.Services;

public class SimpleDomainAgentService : DomainAgentService
{
    public SimpleDomainAgentService(
        TopicRegistry topicRegistry,
        IConversationContext context,
        TopicWorkflowContext wfContext,
        ILogger<DomainAgentService> logger)
        : base(topicRegistry, context, wfContext, logger)
    {
    }

    /// <summary>
    /// Capture the last raw user message into workflow context so
    /// topics like InsuranceBasicsTopic can use it for Q&A loops.
    /// </summary>
    protected override async Task OnUserMessageReceivedAsync(string message, CancellationToken ct)
    {
        _wfContext.SetValue("LastUserMessage", message ?? string.Empty);
        // If a TopicFlow is already active and waiting for input,
        // deliver this message directly into that flow instead of
        // re-running topic selection.
        if (_activeTopic is TopicFlow flow &&
            flow.State == TopicFlow.FlowState.WaitingForInput)
        {
            // Resume the currently waiting flow with this user input
            await flow.ResumeAsync(message, ct);

            // If the flow is no longer waiting after resume, allow it
            // to continue executing any remaining activities in the
            // current run (for example, routing after a RepeatActivity).
            if (flow.State != TopicFlow.FlowState.WaitingForInput)
            {
                await flow.StepAsync(null, ct);
            }

            return;
        }

        await base.OnUserMessageReceivedAsync(message, ct);
    }

    // Basic implementation - no topics registered yet
    protected override async Task StartConversationAsync(CancellationToken ct = default)
    {
        // Log current TopicRegistry contents so we can verify configuration
        var topics = _topicRegistry.GetAllTopics();
        _logger.LogInformation(
            "[SimpleDomainAgentService] StartConversationAsync invoked. TopicRegistry has {Count} topics registered.",
            topics.Count);

        foreach (var t in topics)
        {
            _logger.LogInformation(
                "[SimpleDomainAgentService]   Topic: {Name} (Priority={Priority})",
                t.Name,
                t.Priority);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Wire up UI events from `CustomChatWindowV3` to this agent service.
    /// Allows the demo chat window to invoke the domain event handlers.
    /// </summary>
    public void SubscribeToChatWindowEvents(ConversaCore.UI.Components.CustomChatWindowV3 chatWindow)
    {
        _logger.LogInformation("[SimpleDomainAgentService] Subscribing to CustomChatWindowV3 events");

        chatWindow.ConversationStartRequested += async (s, e) => {
            _logger.LogInformation("[SimpleDomainAgentService] Event received: ConversationStartRequested");
            await OnConversationStartRequestedAsync(e.CancellationToken);
        };

        chatWindow.UserMessageReceived += async (s, e) => {
            _logger.LogInformation("[SimpleDomainAgentService] Event received: UserMessageReceived");
            await OnUserMessageReceivedAsync(e.Message, e.CancellationToken);
        };

        chatWindow.CardSubmitted += async (s, e) => {
            _logger.LogInformation("[SimpleDomainAgentService] Event received: CardSubmitted");
            await OnCardSubmittedAsync(e.Data, e.CancellationToken);
        };

        chatWindow.ConversationResetRequested += async (s, e) => {
            _logger.LogInformation("[SimpleDomainAgentService] Event received: ConversationResetRequested");
            await OnConversationResetRequestedAsync(e.CancellationToken);
        };

        _logger.LogInformation("[SimpleDomainAgentService] ✅ Event subscriptions complete");
    }
}