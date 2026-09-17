using ConversaCore.Runtime;
using Microsoft.Extensions.Logging;

namespace ConversaCore.TopicFlow.Activities;

/// <summary>
/// Dispatches a correlated, versioned, typed interaction request to the host and awaits its typed response
/// without leaking internal workflow context to the containing host.
/// </summary>
/// <typeparam name="TRequest">The serializable request contract understood by the host.</typeparam>
/// <typeparam name="TResponse">The serializable response contract required to resume execution.</typeparam>
public sealed class InvokeHostInteractionActivity<TRequest, TResponse> : TopicFlowActivity
    where TRequest : notnull
    where TResponse : notnull
{
    private readonly string _interactionName;
    private readonly int _version;
    private readonly TimeSpan _timeout;
    private readonly Func<TopicWorkflowContext, TRequest> _requestFactory;
    private readonly IHostInteractionCoordinator _coordinator;
    private readonly string _resultContextKey;

    /// <summary>Creates a correlated host interaction activity.</summary>
    public InvokeHostInteractionActivity(
        string id,
        string interactionName,
        int version,
        TimeSpan timeout,
        IHostInteractionCoordinator coordinator,
        Func<TopicWorkflowContext, TRequest> requestFactory,
        string resultContextKey,
        ILogger<TopicFlowActivity>? logger = null)
        : base(id, logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interactionName);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultContextKey);

        _interactionName = interactionName.Trim();
        _version = version;
        _timeout = timeout;
        _coordinator = coordinator;
        _requestFactory = requestFactory;
        _resultContextKey = resultContextKey.Trim();
    }

    /// <summary>Gets the stable interaction contract name.</summary>
    public string InteractionName => _interactionName;

    /// <summary>Gets the positive interaction contract version.</summary>
    public int Version => _version;

    /// <summary>Gets the maximum duration allowed for the host to respond.</summary>
    public TimeSpan Timeout => _timeout;

    /// <summary>Gets the workflow context key where the typed response is stored.</summary>
    public string ResultContextKey => _resultContextKey;

    /// <inheritdoc />
    protected override async Task<ActivityResult> RunActivity(
        TopicWorkflowContext context,
        object? input = null,
        CancellationToken cancellationToken = default)
    {
        var request = _requestFactory(context);
        var response = await _coordinator.RequestAsync<TRequest, TResponse>(
            _interactionName, _version, request, _timeout, cancellationToken).ConfigureAwait(false);
        context.SetValue(_resultContextKey, response);
        return ActivityResult.Continue(response);
    }
}
