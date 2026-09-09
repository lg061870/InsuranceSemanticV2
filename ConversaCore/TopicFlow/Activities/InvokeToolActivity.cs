using ConversaCore.Tools;
using Microsoft.Extensions.Logging;

namespace ConversaCore.TopicFlow.Activities;

/// <summary>Deterministically invokes one declared tool and stores its typed result.</summary>
/// <typeparam name="TTool">The tool implementation type declared by the activity.</typeparam>
/// <typeparam name="TRequest">The typed request produced from workflow state.</typeparam>
/// <typeparam name="TResult">The typed result stored in workflow state.</typeparam>
/// <remarks>
/// The activity owns only request mapping and result storage. It does not expose the catalog,
/// executor, service provider, or tool internals to the workflow. Tool policy and validation
/// remain enforced by <see cref="IToolExecutor"/>.
/// </remarks>
public sealed class InvokeToolActivity<TTool, TRequest, TResult> : TopicFlowActivity
    where TTool : class, IConversaTool<TRequest, TResult>
{
    private readonly IToolExecutor _executor;
    private readonly string _toolId;
    private readonly Func<TopicWorkflowContext, TRequest> _requestFactory;
    private readonly Func<TopicWorkflowContext, ToolExecutionContext> _executionContextFactory;

    /// <summary>Creates a deterministic invocation activity.</summary>
    public InvokeToolActivity(
        string id,
        string toolId,
        IToolExecutor executor,
        Func<TopicWorkflowContext, TRequest> requestFactory,
        Func<TopicWorkflowContext, ToolExecutionContext> executionContextFactory,
        string resultContextKey,
        ILogger<TopicFlowActivity>? logger = null)
        : base(id, logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(requestFactory);
        ArgumentNullException.ThrowIfNull(executionContextFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultContextKey);
        _toolId = toolId.Trim();
        _executor = executor;
        _requestFactory = requestFactory;
        _executionContextFactory = executionContextFactory;
        ResultContextKey = resultContextKey.Trim();
    }

    /// <summary>Gets the workflow context key containing the <see cref="ToolResult{TResult}"/>.</summary>
    public string ResultContextKey { get; }

    /// <inheritdoc />
    protected override async Task<ActivityResult> RunActivity(
        TopicWorkflowContext context, object? input = null, CancellationToken cancellationToken = default)
    {
        var request = _requestFactory(context);
        var executionContext = _executionContextFactory(context);
        var result = await _executor.ExecuteAsync<TRequest, TResult>(
            _toolId, request, executionContext, cancellationToken).ConfigureAwait(false);
        context.SetValue(ResultContextKey, result);
        return ActivityResult.Continue(result);
    }
}
