using ConversaCore.Registration;

namespace ConversaCore.Runtime;

/// <summary>Routes input without activating topics, delivering input, or mutating a session.</summary>
public interface ITopicRouter
{
    /// <summary>Returns a decision for the runner to execute. Cancellation and scorer failures propagate.</summary>
    Task<TopicRoutingDecision> RouteAsync(TopicRoutingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>The runner's assessment of the active execution for this input.</summary>
public enum ActiveTopicInputState
{
    /// <summary>No active input owner; perform ordinary routing.</summary>
    None,
    /// <summary>Waiting, and has not yet been offered this input.</summary>
    Waiting,
    /// <summary>The active execution already accepted this input; do not deliver it again.</summary>
    Accepted,
    /// <summary>The active execution declined this input; exclude it from candidate ranking.</summary>
    Declined
}

/// <summary>Immutable routing input. Active state comes from the runner, never from the model.</summary>
public sealed record TopicRoutingRequest(string Message, string? ActiveTopicId = null,
    ActiveTopicInputState ActiveState = ActiveTopicInputState.None);

/// <summary>Distinguishes delivery, already-consumed input, new selection, and no match.</summary>
public enum TopicRoutingDecisionKind
{
    /// <summary>Offer this input to the waiting execution before attempting global routing.</summary>
    OfferToActive,
    /// <summary>The runner already delivered the input successfully; take no further action.</summary>
    AlreadyHandled,
    /// <summary>Deterministic metadata matching selected a topic.</summary>
    DeterministicMatch,
    /// <summary>Optional semantic ranking selected a topic.</summary>
    SemanticMatch,
    /// <summary>No candidate qualified; use the explicitly configured system fallback.</summary>
    Fallback,
    /// <summary>No candidate qualified and no fallback was configured.</summary>
    NoMatch
}

/// <summary>A selection, not an activation or a session mutation. Confidence is null for unscored decisions.</summary>
public sealed record TopicRoutingDecision(TopicRoutingDecisionKind Kind, TopicDescriptor? Topic, float? Confidence = null);

/// <summary>Optional semantic ranking seam. Receives only eligible descriptors, not services or live topics.</summary>
/// <remarks>Return scores in [0,1] keyed by the supplied topic IDs. Missing IDs are unscored.
/// Unknown IDs, duplicate case-insensitive IDs, and nonfinite/out-of-range scores are rejected.</remarks>
public interface ITopicSemanticRanker
{
    /// <summary>Ranks a bounded eligible set only after deterministic routing fails.</summary>
    Task<IReadOnlyDictionary<string, float>> RankAsync(string message,
        IReadOnlyList<TopicDescriptor> candidates, CancellationToken cancellationToken = default);
}

/// <summary>One threshold shared by deterministic and semantic routing.</summary>
public sealed record TopicRouterOptions
{
    /// <summary>Inclusive minimum positive confidence; must be finite and in (0,1]. Default 0.5.</summary>
    public float MinimumConfidence { get; init; } = 0.5f;
    /// <summary>Explicit registered system fallback ID. Null disables fallback.</summary>
    public string? FallbackTopicId { get; init; }
    /// <summary>Maximum descriptors exposed to optional semantic ranking, ordered by priority then ID.</summary>
    public int MaxSemanticCandidates { get; init; } = 20;
}
