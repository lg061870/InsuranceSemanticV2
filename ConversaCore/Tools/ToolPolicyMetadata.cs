namespace ConversaCore.Tools;

/// <summary>Classifies whether a tool only reads data or may change durable state.</summary>
public enum ToolSideEffect
{
    /// <summary>The tool does not change durable domain state.</summary>
    ReadOnly,
    /// <summary>The tool may change durable domain state or cause an external side effect.</summary>
    Mutating
}

/// <summary>Declarative authorization requirements for a tool.</summary>
public sealed record ToolAuthorizationPolicy
{
    /// <summary>Optional named policy evaluated by the executor.</summary>
    public string? PolicyName { get; init; }

    /// <summary>Claims the authenticated subject must possess.</summary>
    public IReadOnlySet<string> RequiredClaims { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Declarative confirmation requirement for a tool invocation.</summary>
public sealed record ToolConfirmationPolicy
{
    /// <summary>Gets whether explicit user confirmation is required.</summary>
    public bool Required { get; init; }

    /// <summary>Stable confirmation purpose shown to the host when required.</summary>
    public string? Purpose { get; init; }
}

/// <summary>Declarative retry and timeout limits for one tool.</summary>
public sealed record ToolReliabilityPolicy
{
    /// <summary>Maximum execution duration.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Maximum automatic retries after a classified transient failure.</summary>
    public int MaxRetries { get; init; }

    /// <summary>Optional stable idempotency-key requirement for mutating calls.</summary>
    public bool RequiresIdempotencyKey { get; init; }
}

/// <summary>Declarative data handling and audit metadata for a tool.</summary>
public sealed record ToolDataPolicy
{
    /// <summary>Gets whether request/result payloads require redaction in diagnostics.</summary>
    public bool Sensitive { get; init; }

    /// <summary>Optional stable audit category for invocation records.</summary>
    public string? AuditCategory { get; init; }
}
