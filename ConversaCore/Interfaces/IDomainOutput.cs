namespace ConversaCore.Interfaces;

/// <summary>
/// Represents a structured, machine-readable result produced by a reasoning activity.
/// Used as TOutput in SemanticQueryActivity.
/// </summary>
public interface IDomainOutput {
    /// <summary>
    /// A unique identifier for this result, for storage or reference in context.
    /// </summary>
    string OutputId { get; }

    /// <summary>
    /// Converts the output to JSON for logging, auditing, or re-reasoning.
    /// </summary>
    string ToJson();
}
