namespace ConversaCore.Tools;

/// <summary>Represents one reusable, typed domain operation.</summary>
/// <typeparam name="TRequest">The validated request accepted by the tool.</typeparam>
/// <typeparam name="TResult">The typed value returned when execution succeeds.</typeparam>
/// <remarks>
/// A tool is a capability, not a topic or workflow activity. It does not render UI,
/// select topics, or mutate workflow state directly. The executor and later policy
/// work own validation, authorization, confirmation, timeout, retry, and audit rules.
/// </remarks>
public interface IConversaTool<TRequest, TResult>
{
    /// <summary>Gets immutable metadata describing this tool's public contract.</summary>
    ToolDescriptor Descriptor { get; }

    /// <summary>Executes the operation for a request in the current conversation scope.</summary>
    /// <param name="request">The validated, typed request.</param>
    /// <param name="context">Narrow identity, correlation, and scoped-service context.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A typed success or failure result.</returns>
    ValueTask<ToolResult<TResult>> ExecuteAsync(
        TRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}
