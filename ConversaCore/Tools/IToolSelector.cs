namespace ConversaCore.Tools;

/// <summary>Ranks a bounded, topic-approved set of tool descriptors.</summary>
public interface IToolSemanticRanker
{
    /// <summary>Returns scores keyed by the supplied tool IDs.</summary>
    Task<IReadOnlyDictionary<string, float>> RankAsync(
        string request, IReadOnlyList<ToolDescriptor> candidates, CancellationToken cancellationToken = default);
}

/// <summary>Controls explicit enrollment and minimum confidence for selection.</summary>
public sealed record ToolSelectionOptions
{
    /// <summary>Gets whether this selection point is explicitly enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the inclusive score required for selection.</summary>
    public float MinimumScore { get; init; } = 0.5f;

    /// <summary>Gets the maximum number of descriptors exposed to semantic ranking.</summary>
    public int MaxCandidates { get; init; } = 20;
}

/// <summary>Result of a bounded semantic tool selection.</summary>
public sealed record ToolSelectionResult(ToolDescriptor Tool, float Score);

/// <summary>Selects a tool only from an explicitly declared topic allowlist.</summary>
public interface IToolSelector
{
    /// <summary>Ranks the approved candidates and returns the selected tool, if any.</summary>
    Task<ToolSelectionResult?> SelectAsync(string request, IReadOnlySet<string> allowedToolIds,
        ToolSelectionOptions options, CancellationToken cancellationToken = default);
}
