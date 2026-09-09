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
}
