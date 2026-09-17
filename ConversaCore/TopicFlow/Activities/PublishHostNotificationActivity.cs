using ConversaCore.Runtime;
using Microsoft.Extensions.Logging;

namespace ConversaCore.TopicFlow.Activities;

/// <summary>
/// Publishes an immutable, versioned, typed host notification to the runtime output stream
/// without leaking internal workflow context to the containing host.
/// </summary>
/// <typeparam name="TPayload">The serializable domain contract understood by the host.</typeparam>
public sealed class PublishHostNotificationActivity<TPayload> : TopicFlowActivity
    where TPayload : notnull
{
    private readonly string _eventName;
    private readonly int _version;
    private readonly Func<TopicWorkflowContext, TPayload> _payloadFactory;
    private readonly IConversationOutputDispatcher _dispatcher;
    private readonly IConversationSession _session;

    /// <summary>Creates a one-way host notification activity.</summary>
    public PublishHostNotificationActivity(
        string id,
        string eventName,
        int version,
        Func<TopicWorkflowContext, TPayload> payloadFactory,
        IConversationOutputDispatcher dispatcher,
        IConversationSession session,
        ILogger<TopicFlowActivity>? logger = null)
        : base(id, logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentNullException.ThrowIfNull(payloadFactory);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(session);

        _eventName = eventName.Trim();
        _version = version;
        _payloadFactory = payloadFactory;
        _dispatcher = dispatcher;
        _session = session;
    }

    /// <summary>Gets the stable domain event name.</summary>
    public string EventName => _eventName;

    /// <summary>Gets the positive contract version.</summary>
    public int Version => _version;

    /// <inheritdoc />
    protected override async Task<ActivityResult> RunActivity(
        TopicWorkflowContext context,
        object? input = null,
        CancellationToken cancellationToken = default)
    {
        var payload = _payloadFactory(context);
        await _dispatcher.DispatchAsync(
            new HostNotification<TPayload>(_session.ConversationId, _eventName, _version, payload),
            cancellationToken).ConfigureAwait(false);
        return ActivityResult.Continue(payload);
    }
}
