using ConversaCore.Topics;
using System.Collections.Frozen;

namespace ConversaCore.Registration;

/// <summary>
/// Classifies a topic as a framework-owned system topic (fallback, error handling,
/// escalation, sign-in, and similar cross-cutting concerns, such as the topics under
/// <c>ConversaCore.SystemTopics</c>) or as an ordinary domain-authored business topic.
/// The router uses this classification to reason about topic groups separately from
/// per-topic priority.
/// </summary>
public enum TopicClassification
{
    /// <summary>An ordinary, domain-authored business topic.</summary>
    Domain,

    /// <summary>A framework-owned system topic such as fallback, error, or escalation handling.</summary>
    System
}

/// <summary>
/// Declares whether a topic may be interrupted by another topic's match while it is
/// waiting for user input, or whether it must always be offered first refusal of the
/// next input before any other topic is considered. See target architecture section
/// 7.2 ("Give waiting topics first refusal ... Reroute only when the active topic
/// declines the input or explicitly permits interruption").
/// </summary>
public enum TopicInterruptionPolicy
{
    /// <summary>
    /// The topic must be offered the next input first while it is waiting for input;
    /// routing may move to another topic only if this topic declines to handle the
    /// input. This is the safer default and matches the router's baseline policy.
    /// </summary>
    FirstRefusal,

    /// <summary>
    /// The topic explicitly permits another eligible topic to interrupt it while it is
    /// waiting for input.
    /// </summary>
    Interruptible
}

/// <summary>
/// Immutable identity, description, routing metadata, and factory metadata for a topic.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TopicDescriptor"/> is the framework's stable representation of "what a
/// topic is" and "how to build one," independent of the .NET class implementing it and
/// independent of any human-facing display name. It is pure data plus constructor
/// validation: it performs no side effects, holds no mutable conversation state, and is
/// safe to share as singleton registration metadata (see target architecture section
/// 7.1 — "Separate immutable definitions from mutable instances"). A future scoped
/// activation mechanism (an <c>ITopicActivator</c>) combines a descriptor with the
/// active conversation scope to produce a live, mutable topic instance.
/// </para>
/// <para>
/// <see cref="TopicId"/> is the stable identifier that routing and subtopic references
/// should key off, independent of the implementing class name or
/// <see cref="DisplayName"/>. This directly addresses the WP0 registration defects
/// where a topic's class name and its registered/runtime name diverged (for example,
/// class <c>MarketingT2Topic</c> versus the base name <c>MarketingTypeTwoTopic</c>) and
/// where subtopic calls such as <c>TriggerTopicActivity</c> reference topics only by
/// loosely-validated string names.
/// </para>
/// <para>
/// Equality and hashing are defined solely by <see cref="TopicId"/>, compared
/// case-insensitively (ordinal), because a descriptor's identity for dictionary-keying
/// and duplicate-registration detection is its stable ID — not its display name,
/// priority, or any other descriptive metadata. Two descriptors that differ only in,
/// for example, <see cref="Priority"/> or <see cref="AllowedToolIds"/> are still
/// considered the same topic registration and collide as duplicates, which is the
/// behavior startup registration validation (a future CC-103) needs.
/// </para>
/// </remarks>
public sealed record TopicDescriptor
{
    private IReadOnlySet<string> _triggerPhrases = Array.Empty<string>().ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Exact, case-insensitive routing phrases. Copied, trimmed and frozen at registration;
    /// these are matching hints, not unique topic aliases. Multiple topics may share a phrase.</summary>
    public IReadOnlySet<string> TriggerPhrases
    {
        get => _triggerPhrases;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Trigger phrases must not be blank.", nameof(value));
            _triggerPhrases = value.Select(p => p.Trim()).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static readonly IReadOnlySet<string> EmptyToolIds = new HashSet<string>();

    /// <summary>
    /// The stable, unique identifier for this topic. This is the value routing code,
    /// subtopic references, and duplicate-registration checks key off. It is
    /// independent of the .NET class name implementing the topic and independent of
    /// <see cref="DisplayName"/>, so renaming a class or its human-facing display text
    /// never breaks a routing reference. Must not be null, empty, or whitespace.
    /// </summary>
    public string TopicId { get; }

    /// <summary>
    /// A human-facing name used for diagnostics, logs, and authoring tools. This is
    /// descriptive only — routing and duplicate-detection code must never key off this
    /// value. Defaults to <see cref="TopicId"/> when not supplied or supplied as
    /// null/whitespace.
    /// </summary>
    public string DisplayName { get; init; }

    /// <summary>
    /// A human-facing description of the topic's purpose, used for diagnostics and
    /// authoring tools. Defaults to an empty string.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Routing tie-break priority. Higher values are preferred over lower values when
    /// more than one topic is otherwise eligible to handle the same input, mirroring
    /// the existing ranking behavior of <c>ITopic.Priority</c> and
    /// <c>TopicRegistry.FindBestTopicAsync</c> (which orders candidates by priority,
    /// descending, before evaluating confidence). Defaults to 0.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Classifies this topic as a framework-owned system topic or an ordinary
    /// domain-authored topic. Defaults to <see cref="TopicClassification.Domain"/>.
    /// </summary>
    public TopicClassification Classification { get; init; } = TopicClassification.Domain;

    /// <summary>
    /// Declares whether this topic can be interrupted by another topic while it is
    /// waiting for user input, or whether it must always be given first refusal of the
    /// next input. Defaults to <see cref="TopicInterruptionPolicy.FirstRefusal"/>.
    /// </summary>
    public TopicInterruptionPolicy InterruptionPolicy { get; init; } = TopicInterruptionPolicy.FirstRefusal;

    /// <summary>
    /// The stable tool IDs this topic is permitted to invoke or semantically select
    /// among. Tools do not exist yet in this codebase (they are introduced in a future
    /// work package); this field is reserved data for the tool-allowlist enforcement
    /// described in the target architecture. Defaults to an empty set.
    /// </summary>
    public IReadOnlySet<string> AllowedToolIds { get; init; } = EmptyToolIds;

    /// <summary>
    /// Factory metadata sufficient to construct or activate a topic instance later. This
    /// mirrors how topics are already constructed today — a delegate closing over an
    /// <see cref="IServiceProvider"/>, as used by hosts such as
    /// <c>InsuranceTopicRegistrationExtensions.AddInsuranceTopics</c>
    /// (<c>services.AddScoped&lt;ITopic&gt;(sp =&gt; factory(sp))</c>) — so a future
    /// registration API and scoped topic activator can resolve or construct the topic
    /// instance from the active conversation scope without this descriptor needing to
    /// know the topic's concrete constructor shape. Invoking the factory is outside the
    /// scope of this type; it is stored, not called, here.
    /// </summary>
    public Func<IServiceProvider, ITopic> Factory { get; }

    /// <summary>
    /// Creates a new immutable topic descriptor.
    /// </summary>
    /// <param name="topicId">The stable, unique topic identifier. Must not be null, empty, or whitespace.</param>
    /// <param name="factory">
    /// A factory that resolves or constructs the topic instance from a service provider
    /// scoped to one conversation. Stored as-is; never invoked by this type.
    /// </param>
    /// <param name="displayName">
    /// Human-facing display name for diagnostics/authoring. Defaults to
    /// <paramref name="topicId"/> when null, empty, or whitespace.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="topicId"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    public TopicDescriptor(string topicId, Func<IServiceProvider, ITopic> factory, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(topicId))
            throw new ArgumentException("Topic ID must not be null, empty, or whitespace.", nameof(topicId));

        TopicId = topicId;
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? topicId : displayName;
    }

    /// <summary>
    /// Determines whether this descriptor identifies the same topic as
    /// <paramref name="other"/>. Identity is defined solely by <see cref="TopicId"/>
    /// (case-insensitive ordinal comparison); no other member participates in equality.
    /// </summary>
    /// <param name="other">The descriptor to compare against.</param>
    /// <returns><see langword="true"/> if both descriptors share the same topic ID; otherwise <see langword="false"/>.</returns>
    public bool Equals(TopicDescriptor? other) =>
        other is not null && string.Equals(TopicId, other.TopicId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(TopicDescriptor?)"/>,
    /// derived solely from the case-insensitive <see cref="TopicId"/>.
    /// </summary>
    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(TopicId);
}
