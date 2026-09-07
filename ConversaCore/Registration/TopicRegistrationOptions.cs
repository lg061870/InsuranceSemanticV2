using ConversaCore.Topics;

namespace ConversaCore.Registration;

/// <summary>
/// Mutable configuration surface for the optional <c>configure</c> callback accepted by
/// the <see cref="ConversaCoreBuilderTopicExtensions"/> <c>AddTopic&lt;TTopic&gt;</c>
/// overloads. Mirrors the descriptive, non-identity metadata on
/// <see cref="TopicDescriptor"/> (display name, description, priority, classification,
/// interruption policy, and allowed tool IDs) in a plain mutable object so a caller can
/// set a handful of fields with ordinary property assignment, since
/// <see cref="TopicDescriptor"/> itself is an immutable record and cannot be mutated
/// piecemeal inside a delegate.
/// </summary>
/// <remarks>
/// This type intentionally does not expose <see cref="TopicDescriptor.TopicId"/> or
/// <see cref="TopicDescriptor.Factory"/>: those are identity and construction concerns
/// supplied directly as arguments to the <c>AddTopic</c> call, not descriptive metadata a
/// caller tweaks inside a configuration callback.
/// </remarks>
public sealed class TopicRegistrationOptions
{
    /// <summary>
    /// A human-facing display name for diagnostics and authoring tools. Left
    /// <see langword="null"/> to fall back to <see cref="TopicDescriptor"/>'s own
    /// default (the topic ID).
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// A human-facing description of the topic's purpose. Defaults to
    /// <see cref="string.Empty"/>, matching <see cref="TopicDescriptor.Description"/>'s
    /// default.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Routing tie-break priority; see <see cref="TopicDescriptor.Priority"/>. Defaults
    /// to 0.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// System/domain classification; see <see cref="TopicDescriptor.Classification"/>.
    /// Defaults to <see cref="TopicClassification.Domain"/>.
    /// </summary>
    public TopicClassification Classification { get; set; } = TopicClassification.Domain;

    /// <summary>
    /// Interruption policy; see <see cref="TopicDescriptor.InterruptionPolicy"/>.
    /// Defaults to <see cref="TopicInterruptionPolicy.FirstRefusal"/>.
    /// </summary>
    public TopicInterruptionPolicy InterruptionPolicy { get; set; } = TopicInterruptionPolicy.FirstRefusal;

    /// <summary>
    /// The stable tool IDs this topic may invoke or semantically select among; see
    /// <see cref="TopicDescriptor.AllowedToolIds"/>. Left <see langword="null"/> to fall
    /// back to an empty set.
    /// </summary>
    public IReadOnlySet<string>? AllowedToolIds { get; set; }
}
