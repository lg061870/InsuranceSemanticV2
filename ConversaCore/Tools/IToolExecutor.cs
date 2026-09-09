namespace ConversaCore.Tools;

/// <summary>Executes declared tools with validation and policy enforcement.</summary>
public interface IToolExecutor
{
    /// <summary>Resolves and executes one tool using the supplied trusted context.</summary>
    ValueTask<ToolResult<TResult>> ExecuteAsync<TRequest, TResult>(
        string toolId,
        TRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken = default);
}
