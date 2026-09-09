namespace ConversaCore.UI.Lifecycle;

/// <summary>Tracks legacy .NET event subscriptions and detaches them as one idempotent unit.</summary>
internal sealed class EventSubscriptionScope : IDisposable
{
    private readonly object _gate = new();
    private List<Action>? _detachActions = new();

    public void Add<TEventArgs>(
        Action<EventHandler<TEventArgs>> attach,
        Action<EventHandler<TEventArgs>> detach,
        EventHandler<TEventArgs> handler)
        where TEventArgs : EventArgs
    {
        ArgumentNullException.ThrowIfNull(attach);
        ArgumentNullException.ThrowIfNull(detach);
        ArgumentNullException.ThrowIfNull(handler);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_detachActions is null, this);
            attach(handler);
            _detachActions.Add(() => detach(handler));
        }
    }

    public void Dispose()
    {
        List<Action>? detachActions;
        lock (_gate)
        {
            detachActions = _detachActions;
            _detachActions = null;
        }

        if (detachActions is null)
            return;

        List<Exception>? failures = null;
        for (var index = detachActions.Count - 1; index >= 0; index--)
        {
            try
            {
                detachActions[index]();
            }
            catch (Exception exception)
            {
                (failures ??= new List<Exception>()).Add(exception);
            }
        }

        if (failures is not null)
            throw new AggregateException("One or more event handlers could not be detached.", failures);
    }
}
