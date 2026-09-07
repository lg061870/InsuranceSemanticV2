using ConversaCore.Registration;

namespace ConversaCore.Runtime;

/// <summary>Default scoped implementation of active-topic first-refusal, interruption, and fallback policy.</summary>
public sealed class ConversationMessageCoordinator : IConversationMessageCoordinator
{
    private readonly IConversationSession _session;
    private readonly ITopicRouter _router;
    private readonly IWorkflowRunner _runner;

    /// <summary>Creates a coordinator over one conversation's session, router, and runner.</summary>
    public ConversationMessageCoordinator(IConversationSession session, ITopicRouter router, IWorkflowRunner runner)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    /// <inheritdoc />
    public async Task<ConversationMessageOutcome> ProcessAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_runner.HasActiveExecution)
            return await ExecuteSelectionAsync(await _router.RouteAsync(new(message), cancellationToken).ConfigureAwait(false),
                message, interruption: false, cancellationToken).ConfigureAwait(false);

        var active = _session.ActiveTopic ?? throw new InvalidOperationException(
            "The runner retains an active execution but the conversation session has no active descriptor.");
        var firstDecision = await _router.RouteAsync(
            new TopicRoutingRequest(message, active.TopicId, ActiveTopicInputState.Waiting), cancellationToken).ConfigureAwait(false);

        if (firstDecision.Kind == TopicRoutingDecisionKind.OfferToActive)
        {
            var attempted = await _runner.DeliverToActiveAsync(message, cancellationToken).ConfigureAwait(false);
            if (attempted.State != WorkflowExecutionState.NotHandled)
                return new(firstDecision, attempted);

            var reroute = await _router.RouteAsync(
                new TopicRoutingRequest(message, active.TopicId, ActiveTopicInputState.Declined), cancellationToken).ConfigureAwait(false);
            return await ExecuteSelectionAsync(reroute, message, interruption: true, cancellationToken).ConfigureAwait(false);
        }

        return await ExecuteSelectionAsync(firstDecision, message, interruption: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ConversationMessageOutcome> ExecuteSelectionAsync(TopicRoutingDecision decision, string message,
        bool interruption, CancellationToken cancellationToken)
    {
        if (decision.Kind is TopicRoutingDecisionKind.NoMatch or TopicRoutingDecisionKind.AlreadyHandled)
            return new(decision, null);
        if (decision.Topic is null)
            throw new InvalidOperationException($"Routing decision '{decision.Kind}' did not contain a topic.");

        var execution = interruption
            ? await _runner.InterruptAndDeliverAsync(decision.Topic, message, cancellationToken).ConfigureAwait(false)
            : await _runner.ActivateAndDeliverAsync(decision.Topic, message, cancellationToken).ConfigureAwait(false);
        return new(decision, execution);
    }
}
