namespace ConversaCore.Runtime;

/// <summary>Publishes typed outputs in one conversation scope and creates isolated subscribers.</summary>
public interface IConversationOutputDispatcher : IAsyncDisposable
{
    /// <summary>Publishes one immutable output to every current subscriber in a common order.</summary>
    /// <param name="output">The output, whose conversation ID must match this scope.</param>
    /// <param name="cancellationToken">Cancels publication before the output is committed.</param>
    Task DispatchAsync(ConversationOutput output, CancellationToken cancellationToken = default);

    /// <summary>Creates an independent asynchronous subscription from this point forward.</summary>
    IConversationOutputSubscription Subscribe();
}
