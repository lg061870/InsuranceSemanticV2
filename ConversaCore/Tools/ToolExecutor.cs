using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

namespace ConversaCore.Tools;

/// <summary>Scoped executor that resolves a fresh tool instance for each invocation.</summary>
public sealed class ToolExecutor : IToolExecutor
{
    private readonly IToolCatalog _catalog;
    private readonly ILogger<ToolExecutor> _logger;

    /// <summary>Creates an executor over an immutable catalog.</summary>
    public ToolExecutor(IToolCatalog catalog, ILogger<ToolExecutor> logger)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async ValueTask<ToolResult<TResult>> ExecuteAsync<TRequest, TResult>(
        string toolId, TRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(context);
        if (!_catalog.TryGetDescriptor(toolId, out var descriptor) || descriptor is null)
            return ToolResult<TResult>.Failure("tool_not_declared", "The requested tool is not registered.");
        if (descriptor.RequestType != typeof(TRequest) || descriptor.ResultType != typeof(TResult))
            return ToolResult<TResult>.Failure("tool_type_mismatch", "The request or result type does not match the tool contract.");
        if (request is null)
            return ToolResult<TResult>.Failure("invalid_request", "The tool request is required.");

        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validation, true))
            return ToolResult<TResult>.Failure("invalid_request", "The tool request failed validation.");
        if (descriptor.Authorization.PolicyName is { Length: > 0 } policy && !context.GrantedPolicies.Contains(policy))
            return ToolResult<TResult>.Failure("not_authorized", "The subject is not authorized for this tool.");
        if (descriptor.Authorization.RequiredClaims.Any(claim => !context.GrantedClaims.Contains(claim)))
            return ToolResult<TResult>.Failure("not_authorized", "The subject is missing a required tool claim.");
        if (descriptor.Confirmation.Required && !context.ConfirmationGranted)
            return ToolResult<TResult>.Failure("confirmation_required", "Explicit confirmation is required for this tool.");
        if (descriptor.SideEffect == ToolSideEffect.Mutating && descriptor.Reliability.RequiresIdempotencyKey &&
            string.IsNullOrWhiteSpace(context.IdempotencyKey))
            return ToolResult<TResult>.Failure("idempotency_required", "An idempotency key is required for this operation.");
        if (descriptor.ImplementationType is null)
            return ToolResult<TResult>.Failure("tool_not_activated", "The tool has no registered implementation.");

        try
        {
            var tool = context.Services.GetService(descriptor.ImplementationType);
            if (tool is not IConversaTool<TRequest, TResult> typedTool)
                return ToolResult<TResult>.Failure("tool_type_mismatch", "The registered implementation does not match the tool contract.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(descriptor.Reliability.Timeout);
            _logger.LogInformation("Executing tool {ToolId} version {Version} correlation {CorrelationId}",
                descriptor.ToolId, descriptor.Version, context.CorrelationId);
            return await typedTool.ExecuteAsync(request, context, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ToolResult<TResult>.Failure("timeout", "The tool operation timed out.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Tool {ToolId} failed with an internal error; payloads omitted", descriptor.ToolId);
            return ToolResult<TResult>.Failure("tool_failed", "The tool operation failed.");
        }
    }
}
