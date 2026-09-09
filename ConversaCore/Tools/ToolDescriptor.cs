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
        string? resultSchema = null)
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
}
