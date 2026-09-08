namespace ConversaCore.Runtime;

/// <summary>Coordinates typed, correlated, awaitable interactions with the containing host.</summary>
public interface IHostInteractionCoordinator : IAsyncDisposable
{
    /// <summary>Dispatches a request and awaits exactly one matching typed host response.</summary>
    Task<TResponse> RequestAsync<TRequest, TResponse>(string interactionName, int version,
        TRequest request, TimeSpan timeout, CancellationToken cancellationToken = default)
        where TRequest : notnull
        where TResponse : notnull;

    /// <summary>Validates and completes the exact pending request identified by the response.</summary>
    Task RespondAsync(HostInteractionResponse response, CancellationToken cancellationToken = default);
}

/// <summary>Thrown when a host response does not identify a currently pending interaction.</summary>
public sealed class HostInteractionNotPendingException(string requestId)
    : InvalidOperationException($"Host interaction '{requestId}' is unknown, expired, late, or already completed.")
{
    /// <summary>Gets the rejected correlation identifier.</summary>
    public string RequestId { get; } = requestId;
}

/// <summary>Thrown when a response payload cannot satisfy the request's declared response contract.</summary>
public sealed class HostInteractionResponseTypeException(string requestId, Type expectedType, Exception innerException)
    : InvalidOperationException(
        $"Host interaction '{requestId}' requires response type '{expectedType.FullName}'.", innerException)
{
    /// <summary>Gets the rejected correlation identifier.</summary>
    public string RequestId { get; } = requestId;
    /// <summary>Gets the response contract declared by the request.</summary>
    public Type ExpectedType { get; } = expectedType;
}

/// <summary>Thrown when a pending host interaction reaches its declared timeout.</summary>
public sealed class HostInteractionTimeoutException(string requestId, TimeSpan timeout, Exception innerException)
    : TimeoutException($"Host interaction '{requestId}' did not complete within {timeout}.", innerException)
{
    /// <summary>Gets the expired correlation identifier.</summary>
    public string RequestId { get; } = requestId;
    /// <summary>Gets the timeout declared by the request.</summary>
    public TimeSpan Timeout { get; } = timeout;
}
