using ConversaCore.Registration;

namespace ConversaCore.Runtime;

/// <summary>Catalog-backed routing with first refusal, deterministic matching, and optional bounded semantic ranking.</summary>
/// <remarks>Exact ID or exact declared trigger phrase matches score 1. Ties use descending priority,
/// then ordinal-ignore-case ID. System topics never participate in global ranking. This class does
/// not call legacy CanHandleAsync, construct topics, log message payloads, or change session state.
/// The runner owns delivery and pause/resume (CC-205/207); legacy hosts remain unchanged.</remarks>
public sealed class TopicRouter : ITopicRouter
{
    private readonly ITopicCatalog _catalog;
    private readonly TopicRouterOptions _options;
    private readonly ITopicSemanticRanker? _semanticRanker;
    private readonly TopicDescriptor? _fallback;

    /// <summary>Creates a router. A configured fallback must exist and be classified as System.</summary>
    public TopicRouter(ITopicCatalog catalog, TopicRouterOptions? options = null,
        ITopicSemanticRanker? semanticRanker = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _options = options ?? new TopicRouterOptions();
        _semanticRanker = semanticRanker;
        if (!float.IsFinite(_options.MinimumConfidence) || _options.MinimumConfidence <= 0 || _options.MinimumConfidence > 1)
            throw new ArgumentOutOfRangeException(nameof(options), "MinimumConfidence must be finite and in (0,1].");
        if (_options.MaxSemanticCandidates <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxSemanticCandidates must be positive.");
        if (_options.FallbackTopicId is not null)
        {
            if (!_catalog.TryGetDescriptor(_options.FallbackTopicId, out _fallback) ||
                _fallback?.Classification != TopicClassification.System)
                throw new ArgumentException("Fallback must identify a registered system topic.", nameof(options));
        }
    }

    /// <inheritdoc />
    public async Task<TopicRoutingDecision> RouteAsync(TopicRoutingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Message);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(request.ActiveState))
            throw new ArgumentOutOfRangeException(nameof(request), "Unknown active input state.");

        TopicDescriptor? active = null;
        if (request.ActiveTopicId is not null && !_catalog.TryGetDescriptor(request.ActiveTopicId, out active))
            throw new ArgumentException("Active topic is not registered.", nameof(request));
        if (request.ActiveState != ActiveTopicInputState.None && active is null)
            throw new ArgumentException("Active input state requires an active topic ID.", nameof(request));

        if (request.ActiveState == ActiveTopicInputState.Accepted)
            return new(TopicRoutingDecisionKind.AlreadyHandled, active);
        if (request.ActiveState == ActiveTopicInputState.Waiting &&
            active!.InterruptionPolicy == TopicInterruptionPolicy.FirstRefusal)
            return new(TopicRoutingDecisionKind.OfferToActive, active);

        var candidates = _catalog.Descriptors
            .Where(t => t.Classification == TopicClassification.Domain &&
                !(request.ActiveState == ActiveTopicInputState.Declined && t.Equals(active)))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.TopicId, StringComparer.OrdinalIgnoreCase).ToArray();
        var input = request.Message.Trim();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(input, candidate.TopicId, StringComparison.OrdinalIgnoreCase) ||
                candidate.TriggerPhrases.Contains(input))
                return new(TopicRoutingDecisionKind.DeterministicMatch, candidate, 1f);
        }

        if (_semanticRanker is not null && candidates.Length > 0 && input.Length > 0)
        {
            var bounded = Array.AsReadOnly(candidates.Take(_options.MaxSemanticCandidates).ToArray());
            var scores = await _semanticRanker.RankAsync(request.Message, bounded, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (scores is null) throw new InvalidOperationException("Semantic ranker returned null scores.");
            var eligibleIds = bounded.Select(t => t.TopicId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var validated = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, score) in scores)
            {
                if (!eligibleIds.Contains(id) || !float.IsFinite(score) || score < 0 || score > 1 ||
                    !validated.TryAdd(id, score))
                    throw new InvalidOperationException("Semantic ranker returned invalid or ineligible scores.");
            }
            var best = bounded.Where(t => validated.TryGetValue(t.TopicId, out var score) && score >= _options.MinimumConfidence)
                .OrderByDescending(t => validated[t.TopicId]).ThenByDescending(t => t.Priority)
                .ThenBy(t => t.TopicId, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (best is not null)
                return new(TopicRoutingDecisionKind.SemanticMatch, best, validated[best.TopicId]);
        }

        // Interruptible means permission to consider other topics, not permission to discard an unmatched input owner.
        if (request.ActiveState == ActiveTopicInputState.Waiting)
            return new(TopicRoutingDecisionKind.OfferToActive, active);
        return _fallback is null ? new(TopicRoutingDecisionKind.NoMatch, null) : new(TopicRoutingDecisionKind.Fallback, _fallback);
    }
}
