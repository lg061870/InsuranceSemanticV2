namespace ConversaCore.Tools;

/// <summary>Immutable metadata identifying a typed tool capability.</summary>
/// <remarks>
/// Descriptors are registration metadata only. They contain no tool instance or
/// conversation state and are safe for singleton catalogs. Authorization, confirmation,
/// side-effect, timeout, retry, and idempotency metadata are added by CC-401.
/// </remarks>
public sealed record ToolDescriptor
{
    /// <summary>Creates a descriptor for one request/result type pair.</summary>
    public ToolDescriptor(
        string toolId,
        string version,
        string displayName,
        string description,
        Type requestType,
        Type resultType,
        string? requestSchema = null,
        string? resultSchema = null,
        ToolSideEffect sideEffect = ToolSideEffect.ReadOnly,
        ToolAuthorizationPolicy? authorization = null,
        ToolConfirmationPolicy? confirmation = null,
        ToolReliabilityPolicy? reliability = null,
        ToolDataPolicy? dataPolicy = null,
        Type? implementationType = null)
    {
        if (string.IsNullOrWhiteSpace(toolId))
            throw new ArgumentException("Tool ID must not be null, empty, or whitespace.", nameof(toolId));
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Tool version must not be null, empty, or whitespace.", nameof(version));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Tool display name must not be null, empty, or whitespace.", nameof(displayName));
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(resultType);

        ToolId = toolId.Trim();
        Version = version.Trim();
        DisplayName = displayName.Trim();
        Description = description?.Trim() ?? string.Empty;
        RequestType = requestType;
        ResultType = resultType;
        RequestSchema = string.IsNullOrWhiteSpace(requestSchema) ? null : requestSchema.Trim();
        ResultSchema = string.IsNullOrWhiteSpace(resultSchema) ? null : resultSchema.Trim();
        if (!Enum.IsDefined(sideEffect))
            throw new ArgumentOutOfRangeException(nameof(sideEffect));
        if (reliability is not null && (reliability.Timeout <= TimeSpan.Zero || reliability.MaxRetries < 0))
            throw new ArgumentException("Tool timeout must be positive and retry count must not be negative.", nameof(reliability));
        if (confirmation?.Required == true && string.IsNullOrWhiteSpace(confirmation.Purpose))
            throw new ArgumentException("A confirming tool must declare a confirmation purpose.", nameof(confirmation));
        SideEffect = sideEffect;
        Authorization = authorization ?? new ToolAuthorizationPolicy();
        Confirmation = confirmation ?? new ToolConfirmationPolicy();
        Reliability = reliability ?? new ToolReliabilityPolicy();
        DataPolicy = dataPolicy ?? new ToolDataPolicy();
        ImplementationType = implementationType;
    }

    /// <summary>Gets the stable identifier used by registration and allowlists.</summary>
    public string ToolId { get; }

    /// <summary>Gets the compatibility version of this tool contract.</summary>
    public string Version { get; }

    /// <summary>Gets the human-facing tool name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the human- and authoring-facing description.</summary>
    public string Description { get; }

    /// <summary>Gets the CLR request type.</summary>
    public Type RequestType { get; }

    /// <summary>Gets the CLR result type.</summary>
    public Type ResultType { get; }

    /// <summary>Gets optional serialized request schema metadata.</summary>
    public string? RequestSchema { get; }

    /// <summary>Gets optional serialized result schema metadata.</summary>
    public string? ResultSchema { get; }

    /// <summary>Gets the read-only or mutating side-effect classification.</summary>
    public ToolSideEffect SideEffect { get; }

    /// <summary>Gets the authorization requirements evaluated by the executor.</summary>
    public ToolAuthorizationPolicy Authorization { get; }

    /// <summary>Gets the explicit confirmation requirement.</summary>
    public ToolConfirmationPolicy Confirmation { get; }

    /// <summary>Gets timeout, retry, and idempotency requirements.</summary>
    public ToolReliabilityPolicy Reliability { get; }

    /// <summary>Gets sensitivity and audit metadata.</summary>
    public ToolDataPolicy DataPolicy { get; }

    /// <summary>Gets the implementation type, when registered by assembly scan.</summary>
    public Type? ImplementationType { get; init; }
}
