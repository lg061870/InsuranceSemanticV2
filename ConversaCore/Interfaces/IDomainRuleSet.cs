namespace ConversaCore.Interfaces;

/// <summary>
/// Represents a serializable, domain-specific collection of rules that can be
/// consumed by semantic reasoning activities.
/// </summary>
public interface IDomainRuleSet {
    /// <summary>
    /// Returns a unique identifier for this rule set (e.g., "insurance-rules").
    /// </summary>
    string RuleSetId { get; }

    /// <summary>
    /// Returns a short description or purpose (optional, for logging or metadata).
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Serializes the entire rule set into a JSON representation.
    /// This JSON will be passed directly to the AI model.
    /// </summary>
    string ToJson();

    /// <summary>
    /// Returns lightweight metadata for vector indexing or classification.
    /// </summary>
    Dictionary<string, object>? GetMetadata();
}
