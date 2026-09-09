namespace ConversaCore.Tools;
using System.Collections.Concurrent;

/// <summary>Bounded selector that never exposes the global catalog to the ranker.</summary>
public sealed class ToolSelector : IToolSelector
{
    private readonly IToolCatalog _catalog;
    private readonly IToolSemanticRanker _ranker;
    private readonly ConcurrentDictionary<string, ToolDescriptor[]> _candidateCache = new(StringComparer.Ordinal);

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
        if (options.MaxCandidates <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum candidates must be positive.");

        var cacheKey = string.Join('\u001f', allowedToolIds.Order(StringComparer.OrdinalIgnoreCase));
        var bounded = _candidateCache.GetOrAdd(cacheKey, _ => _catalog.Descriptors
            .Where(d => allowedToolIds.Contains(d.ToolId))
            .OrderBy(d => d.ToolId, StringComparer.OrdinalIgnoreCase)
            .ToArray());
        var words = request.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = bounded.Where(d => words.Any(word =>
            d.ToolId.Contains(word, StringComparison.OrdinalIgnoreCase) ||
            d.DisplayName.Contains(word, StringComparison.OrdinalIgnoreCase) ||
            d.Description.Contains(word, StringComparison.OrdinalIgnoreCase))).ToArray();
        var candidates = (matches.Length == 0 ? bounded : matches).Take(options.MaxCandidates).ToArray();
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
