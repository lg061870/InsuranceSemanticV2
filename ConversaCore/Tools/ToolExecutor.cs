using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ConversaCore.Tools;

/// <summary>Scoped executor that resolves a fresh tool instance for each invocation.</summary>
public sealed class ToolExecutor : IToolExecutor
{
    private readonly IToolCatalog _catalog;
    private readonly ILogger<ToolExecutor> _logger;
    private readonly IToolDiagnostics? _diagnostics;

    /// <summary>Creates an executor over an immutable catalog.</summary>
    public ToolExecutor(IToolCatalog catalog, ILogger<ToolExecutor> logger, IToolDiagnostics? diagnostics = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _diagnostics = diagnostics;
    }

    /// <inheritdoc />
    public async ValueTask<ToolResult<TResult>> ExecuteAsync<TRequest, TResult>(
        string toolId, TRequest request, ToolExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        ArgumentNullException.ThrowIfNull(context);
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(context.ConversationId) || string.IsNullOrWhiteSpace(context.Subject) ||
            string.IsNullOrWhiteSpace(context.CorrelationId))
            return ToolResult<TResult>.Failure("invalid_execution_context", "The trusted execution context is incomplete.");
        if (!_catalog.TryGetDescriptor(toolId, out var descriptor) || descriptor is null)
            return ToolResult<TResult>.Failure("tool_not_declared", "The requested tool is not registered.");
        void Emit(ToolDiagnosticKind kind, string? errorCode = null) {
            try { _diagnostics?.Record(new ToolDiagnostic(kind, descriptor.ToolId, descriptor.Version,
                context.CorrelationId, stopwatch.Elapsed, errorCode, descriptor.DataPolicy.Sensitive)); }
            catch (Exception exception) { _logger.LogWarning(exception, "Tool diagnostic sink failed for {ToolId}", descriptor.ToolId); }
        }
        ToolResult<TResult> Reject(string code, string message) { Emit(ToolDiagnosticKind.PolicyRejected, code); return ToolResult<TResult>.Failure(code, message); }
        Emit(ToolDiagnosticKind.Invoking);
        if (context.AllowedToolIds.Count > 0 && !context.AllowedToolIds.Contains(descriptor.ToolId))
            return Reject("tool_not_allowed", "The current topic has not declared this tool.");
        if (descriptor.RequestType != typeof(TRequest) || descriptor.ResultType != typeof(TResult))
            return Reject("tool_type_mismatch", "The request or result type does not match the tool contract.");
        if (request is null)
            return Reject("invalid_request", "The tool request is required.");

        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validation, true))
            return Reject("invalid_request", "The tool request failed validation.");
        if (descriptor.Authorization.PolicyName is { Length: > 0 } policy && !context.GrantedPolicies.Contains(policy))
            return Reject("not_authorized", "The subject is not authorized for this tool.");
        if (descriptor.Authorization.RequiredClaims.Any(claim => !context.GrantedClaims.Contains(claim)))
            return Reject("not_authorized", "The subject is missing a required tool claim.");
        if (descriptor.Confirmation.Required && !context.ConfirmationGranted)
            return Reject("confirmation_required", "Explicit confirmation is required for this tool.");
        if (descriptor.SideEffect == ToolSideEffect.Mutating)
        {
            if (!context.TrustedIdentityValidated)
                return Reject("identity_not_validated", "The subject identity was not validated for this operation.");
            if (descriptor.Reliability.RequiresIdempotencyKey && string.IsNullOrWhiteSpace(context.IdempotencyKey))
                return Reject("idempotency_required", "An idempotency key is required for this operation.");
        }
        if (descriptor.ImplementationType is null)
            return Reject("tool_not_activated", "The tool has no registered implementation.");

        try
        {
            var tool = context.Services.GetService(descriptor.ImplementationType);
            if (tool is not IConversaTool<TRequest, TResult> typedTool)
                return Reject("tool_type_mismatch", "The registered implementation does not match the tool contract.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(descriptor.Reliability.Timeout);
            _logger.LogInformation("Executing tool {ToolId} version {Version} correlation {CorrelationId}",
                descriptor.ToolId, descriptor.Version, context.CorrelationId);
            var result = await typedTool.ExecuteAsync(request, context, timeout.Token).ConfigureAwait(false);
            Emit(ToolDiagnosticKind.Completed);
            Emit(ToolDiagnosticKind.Latency);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            Emit(ToolDiagnosticKind.Failed, "timeout");
            return ToolResult<TResult>.Failure("timeout", "The tool operation timed out.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Tool {ToolId} failed with an internal error; payloads omitted", descriptor.ToolId);
            Emit(ToolDiagnosticKind.Failed, "tool_failed");
            return ToolResult<TResult>.Failure("tool_failed", "The tool operation failed.");
        }
    }
}
