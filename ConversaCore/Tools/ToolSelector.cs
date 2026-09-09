namespace ConversaCore.Tools;

/// <summary>Bounded selector that never exposes the global catalog to the ranker.</summary>
public sealed class ToolSelector : IToolSelector
{
    private readonly IToolCatalog _catalog;
    private readonly IToolSemanticRanker _ranker;

    /// <summary>Creates a selector over an immutable catalog and semantic ranker.</summary>
    public ToolSelector(IToolCatalog catalog, IToolSemanticRanker ranker)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _ranker = ranker ?? throw new ArgumentNullException(nameof(ranker));
    }

    /// <inheritdoc />
    public async Task<ToolSelectionResult?> SelectAsync(string request, IReadOnlySet<string> allowedToolIds,
        ToolSelectionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request);
        ArgumentNullException.ThrowIfNull(allowedToolIds);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.Enabled) return null;
        if (float.IsNaN(options.MinimumScore) || options.MinimumScore is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(options), "Minimum score must be finite and within [0,1].");

        var candidates = _catalog.Descriptors
            .Where(d => allowedToolIds.Contains(d.ToolId))
            .ToArray();
        if (candidates.Length == 0) return null;
        var scores = await _ranker.RankAsync(request, candidates, cancellationToken).ConfigureAwait(false);
        var candidateIds = candidates.Select(c => c.ToolId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in scores)
        {
            if (!candidateIds.Contains(pair.Key) || float.IsNaN(pair.Value) || float.IsInfinity(pair.Value) || pair.Value is < 0 or > 1)
                throw new InvalidOperationException("The semantic ranker returned an invalid tool score.");
        }
        var selected = candidates
            .Select(d => (Descriptor: d, Score: scores.TryGetValue(d.ToolId, out var score) ? score : 0f))
            .Where(item => item.Score >= options.MinimumScore)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Descriptor.ToolId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return selected.Descriptor is null ? null : new ToolSelectionResult(selected.Descriptor, selected.Score);
    }
}
