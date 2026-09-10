using ConversaCore.Runtime;
using ConversaCore.TopicFlow;

namespace InsuranceAgent.Activities;

/// <summary>Publishes one immutable, versioned insurance notification to the containing host.</summary>
public sealed class InsuranceHostNotificationActivity<TPayload> : TopicFlowActivity
    where TPayload : notnull
{
    private readonly string _eventName;
    private readonly Func<TopicWorkflowContext, TPayload> _payloadFactory;
    private readonly IConversationOutputDispatcher _dispatcher;
    private readonly IConversationSession _session;

    /// <summary>Creates a one-way host-notification activity.</summary>
    public InsuranceHostNotificationActivity(
        string id,
        string eventName,
        Func<TopicWorkflowContext, TPayload> payloadFactory,
        IConversationOutputDispatcher dispatcher,
        IConversationSession session)
        : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        _eventName = eventName;
        _payloadFactory = payloadFactory ?? throw new ArgumentNullException(nameof(payloadFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <inheritdoc />
    protected override async Task<ActivityResult> RunActivity(
        TopicWorkflowContext context,
        object? input = null,
        CancellationToken cancellationToken = default)
    {
        var payload = _payloadFactory(context);
        await _dispatcher.DispatchAsync(
            new HostNotification<TPayload>(_session.ConversationId, _eventName, 1, payload),
            cancellationToken).ConfigureAwait(false);
        return ActivityResult.Continue(payload);
    }
}
