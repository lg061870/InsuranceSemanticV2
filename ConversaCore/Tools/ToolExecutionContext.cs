namespace ConversaCore.Tools;

/// <summary>Trusted execution information supplied to a tool by the framework.</summary>
/// <remarks>
/// The service provider is the current conversation scope, allowing a tool to resolve its
/// domain dependencies. This context exposes no UI component, topic instance, workflow
/// activity, or mutable workflow context. The executor—not the tool or model—creates it.
/// </remarks>
public sealed record ToolExecutionContext
{
    /// <summary>Gets the stable conversation identifier.</summary>
    public required string ConversationId { get; init; }

    /// <summary>Gets the authenticated subject identifier.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets the correlation identifier for this invocation.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>Gets the scoped service provider for domain dependencies.</summary>
    public required IServiceProvider Services { get; init; }

    /// <summary>Gets trusted authorization policies granted to the subject.</summary>
    public IReadOnlySet<string> GrantedPolicies { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets trusted claims granted to the subject.</summary>
    public IReadOnlySet<string> GrantedClaims { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets whether the host recorded the declared confirmation.</summary>
    public bool ConfirmationGranted { get; init; }

    /// <summary>Gets the caller-supplied idempotency key, when applicable.</summary>
    public string? IdempotencyKey { get; init; }
}
