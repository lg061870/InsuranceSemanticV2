using ConversaCore.Registration;

namespace ConversaCore.Runtime;

/// <summary>
/// An immutable, read-only, side-effect-free view over a fixed set of registered
/// <see cref="TopicDescriptor"/>s. This is CC-202's implementation of the "immutable
/// <c>ITopicCatalog</c>" described by target architecture section 7.1 ("Separate immutable
/// definitions from mutable instances... An immutable <c>ITopicCatalog</c> may be
/// singleton. It must not store scoped topic instances") and by the work breakdown's
/// CC-202 task text: "Implement immutable <c>ITopicCatalog</c>. Store descriptors and
/// activation metadata only."
/// </summary>
/// <remarks>
/// <para><b>What this type is.</b></para>
/// <para>
/// A <see cref="ITopicCatalog"/> wraps the full resolved set of <see cref="TopicDescriptor"/>
/// registrations a host has already built via CC-100 through CC-105 (<see cref="ConversaCoreBuilder"/>,
/// its <c>AddTopic</c>/<c>AddTopicsFromAssemblyContaining</c> extensions, and
/// <see cref="TopicRegistrationValidator"/>) and gives a consumer a fast, read-only way to
/// look descriptors up — by <see cref="TopicDescriptor.TopicId"/>, or by full enumeration.
/// It performs no registration of its own (that remains <see cref="ConversaCoreBuilder"/>'s
/// job — see <see cref="ConversaCoreBuilderTopicExtensions"/>), resolves nothing from a
/// service scope, and never invokes <see cref="TopicDescriptor.Factory"/>. It is data plus
/// lookup, nothing else, matching the target architecture's DI lifetimes table
/// (section 13): <c>ITopicCatalog</c> is Singleton, holding "Immutable descriptors and
/// factories only."
/// </para>
/// <para><b>Where a catalog instance's descriptors come from.</b></para>
/// <para>
/// CC-104's acceptance evidence (see the "CC-104 acceptance evidence" remarks in
/// <c>ConversaCore.Tests.Registration.TopicRegistrationInstanceSeparationTests</c>)
/// explicitly anticipates this type: "CC-202's <c>ITopicCatalog</c> should wrap that
/// resolution rather than re-deriving descriptors another way." That resolution is
/// <c>IServiceProvider.GetServices&lt;TopicDescriptor&gt;()</c> (equivalently, constructor
/// injection of <c>IEnumerable&lt;TopicDescriptor&gt;</c>), which returns the full set of
/// singleton-instance <see cref="TopicDescriptor"/> registrations CC-102's
/// <c>AddTopic</c> family added to the <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/>.
/// A concrete <see cref="TopicCatalog"/> is constructed directly from that
/// <see cref="IEnumerable{T}"/> of <see cref="TopicDescriptor"/> — the exact input shape
/// this interface's remarks and the CC-202 ticket describe. Wiring a <see cref="TopicCatalog"/>
/// instance into DI (for example, <c>services.AddSingleton&lt;ITopicCatalog&gt;(sp =&gt;
/// new TopicCatalog(sp.GetServices&lt;TopicDescriptor&gt;()))</c>) is a later integration
/// ticket, not this one — see the CC-202 scope note in <see cref="TopicCatalog"/> remarks.
/// </para>
/// <para><b>Deliberately out of scope: routing/eligibility policy.</b></para>
/// <para>
/// Target architecture section 7.2 describes eligibility filtering — ranking eligible
/// topic descriptors by deterministic matching first, then optionally by semantic
/// ranking, then applying thresholds and policy — as work for a future <c>ITopicRouter</c>
/// (CC-204), explicitly listed as out of scope for this ticket ("Do not implement...
/// <c>ITopicRouter</c> (CC-204)... or any routing/ranking policy logic beyond exposing the
/// read-only data a future router will consume"). This interface was evaluated for a
/// convenience classification filter (for example, "all <see cref="TopicClassification.Domain"/>-classified
/// topics" or "all <see cref="TopicClassification.System"/>-classified topics") and
/// deliberately does not add one: <see cref="TopicDescriptor"/>'s existing fields
/// (<see cref="TopicDescriptor.Classification"/>, <see cref="TopicDescriptor.Priority"/>,
/// <see cref="TopicDescriptor.InterruptionPolicy"/>, <see cref="TopicDescriptor.AllowedToolIds"/>)
/// are already public, already typed, and already fully available on every element of
/// <see cref="Descriptors"/> via ordinary LINQ (<c>catalog.Descriptors.Where(d =&gt;
/// d.Classification == TopicClassification.System)</c>) — a bespoke
/// <c>GetByClassification</c>-shaped method on the catalog would only rename that LINQ
/// call, not add capability, and risks quietly encoding a first slice of CC-204's ranking
/// policy (which field(s) to filter/rank by, and in what order) into a ticket that owns
/// storage and lookup, not policy. <see cref="Descriptors"/> is therefore the single,
/// sufficient surface a future <c>ITopicRouter</c> needs for eligibility filtering; no
/// further convenience accessor is added here.
/// </para>
/// <para><b>Duplicate topic IDs at construction.</b></para>
/// <para>
/// See <see cref="TopicCatalog"/> remarks for the full construction-time duplicate-ID
/// decision and its justification.
/// </para>
/// </remarks>
public interface ITopicCatalog
{
    /// <summary>
    /// The number of distinct topics in this catalog. Equivalent to
    /// <c>Descriptors.Count</c>, provided directly so a caller checking only the count does
    /// not need to materialize or enumerate <see cref="Descriptors"/>.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Every <see cref="TopicDescriptor"/> registered in this catalog, in a fixed,
    /// unspecified-but-stable order established at construction time. This is a read-only
    /// snapshot: it does not change after the catalog is constructed, even if the
    /// collection originally supplied to the constructor is later mutated (see
    /// <see cref="TopicCatalog"/> remarks for how immutability is achieved). Intended as
    /// the full-enumeration accessor a future <c>ITopicRouter</c> (CC-204) filters and
    /// ranks eligible topics from directly, per target architecture section 7.2.
    /// </summary>
    IReadOnlyCollection<TopicDescriptor> Descriptors { get; }

    /// <summary>
    /// Determines whether a topic with the given <paramref name="topicId"/> is registered
    /// in this catalog. Comparison is case-insensitive (ordinal), matching
    /// <see cref="TopicDescriptor"/>'s own equality semantics (CC-101). Never throws for an
    /// unrecognized ID — "does this topic exist" is a normal query, not an error condition.
    /// </summary>
    /// <param name="topicId">The topic ID to check.</param>
    /// <returns><see langword="true"/> if a matching topic is registered; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="topicId"/> is null, empty, or consists only of
    /// whitespace — a structurally invalid ID, not merely an absent one.
    /// </exception>
    bool Contains(string topicId);

    /// <summary>
    /// Looks up the <see cref="TopicDescriptor"/> registered under
    /// <paramref name="topicId"/>. Comparison is case-insensitive (ordinal), matching
    /// <see cref="TopicDescriptor"/>'s own equality semantics (CC-101). Returns
    /// <see langword="false"/> (with <paramref name="descriptor"/> set to
    /// <see langword="null"/>) rather than throwing when no topic with that ID is
    /// registered — an unrecognized ID is a normal, expected outcome of a lookup, not an
    /// error condition, mirroring the "returns false rather than throwing for an
    /// unrecognized ID" convention already established by
    /// <c>IConversationSession.TryResolvePendingHostInteraction</c> (CC-201).
    /// </summary>
    /// <param name="topicId">The topic ID to look up.</param>
    /// <param name="descriptor">
    /// The matching descriptor, when found; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a matching topic was found; otherwise <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="topicId"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    bool TryGetDescriptor(string topicId, out TopicDescriptor? descriptor);
}
