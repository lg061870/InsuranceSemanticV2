using ConversaCore.Registration;

namespace ConversaCore.Runtime;

/// <summary>
/// Default, immutable implementation of <see cref="ITopicCatalog"/> (CC-202). Wraps a
/// fixed snapshot of the <see cref="TopicDescriptor"/>s it is constructed with and answers
/// lookups against that snapshot only. See <see cref="ITopicCatalog"/> remarks for this
/// type's overall role and scope boundary against a future <c>ITopicRouter</c> (CC-204).
/// </summary>
/// <remarks>
/// <para><b>Immutability and defensive copying.</b></para>
/// <para>
/// The constructor enumerates <c>descriptors</c> exactly once and copies the result into
/// this instance's own private storage (a <see cref="Dictionary{TKey,TValue}"/> keyed by
/// <see cref="TopicDescriptor.TopicId"/> with <see cref="StringComparer.OrdinalIgnoreCase"/>,
/// plus a materialized array for <see cref="Descriptors"/>). Nothing about this type holds
/// a reference to the caller's original collection after construction returns. This means:
/// </para>
/// <list type="bullet">
/// <item><description>
/// If the caller passes a mutable <see cref="List{T}"/> (or any other mutable
/// <see cref="IEnumerable{T}"/>) and later adds to, removes from, or clears it, this
/// catalog's contents are unaffected — it already copied what it needed at construction
/// time. This is the defensive-copy choice this type makes, and it is the only choice
/// consistent with "immutable" in this type's name and target architecture section 7.1's
/// requirement that the catalog not silently change out from under a consumer (for
/// example, a router mid-request) that is holding a reference to it. The alternative —
/// wrapping the caller's collection directly and relying on the caller never mutating it
/// — is not defensive and is not chosen here.
/// </description></item>
/// <item><description>
/// If <c>descriptors</c> is a lazy sequence (for example, a LINQ query or a generator),
/// it is enumerated exactly once, during construction; re-enumerating it later (if the
/// caller does so independently, for some other purpose) has no effect on this catalog,
/// which never re-reads its source after construction.
/// </description></item>
/// </list>
/// <para><b>Duplicate topic IDs: reject immediately, at construction.</b></para>
/// <para>
/// CC-102's <see cref="ConversaCoreBuilderTopicExtensions.AddTopic(ConversaCoreBuilder, TopicDescriptor)"/>
/// already throws immediately on a duplicate <see cref="TopicDescriptor.TopicId"/> at
/// registration time, and CC-103's <see cref="TopicRegistrationValidator"/> re-checks the
/// full resolved set independently as a defense-in-depth aggregated validation pass. Both
/// treat a duplicate ID as always an error — never resolved by "last one wins" or "first
/// one wins" silently picking a survivor. This constructor makes the same choice for the
/// same reason: a <see cref="TopicCatalog"/> can be constructed directly from a raw
/// <see cref="IEnumerable{T}"/> of <see cref="TopicDescriptor"/> — by a test, by an
/// unusual host, or by any code path that bypasses <see cref="ConversaCoreBuilder"/>
/// entirely — so this type cannot assume CC-102/CC-103's guards already ran. Silently
/// keeping only one of two colliding descriptors would let a consumer resolve completely
/// different behavior for the same <see cref="TopicDescriptor.TopicId"/> depending on
/// registration order, an outcome CC-102/CC-103 both already reject and this type has no
/// principled reason to treat differently just because it is the last stop before a
/// consumer starts looking descriptors up. The alternative (accepting duplicates and
/// picking a "winner" for lookups by ID, while still exposing every duplicate via
/// <see cref="Descriptors"/>) was considered and rejected: it would make <see cref="Descriptors"/>
/// and <see cref="TryGetDescriptor"/>/<see cref="Contains"/> tell inconsistent stories
/// about how many topics with a given ID exist, for no benefit — nothing in the work
/// breakdown or target architecture calls for a catalog that tolerates ambiguous topic
/// identity.
/// </para>
/// <para>
/// The thrown exception is <see cref="ArgumentException"/> (naming the <c>descriptors</c>
/// parameter), not <see cref="InvalidOperationException"/>. This differs from
/// <see cref="ConversaCoreBuilderTopicExtensions.AddTopic(ConversaCoreBuilder, TopicDescriptor)"/>,
/// which throws <see cref="InvalidOperationException"/> — but that method is rejecting a
/// new registration against pre-existing accumulated state (the builder's underlying
/// service collection), an operation/state conflict. This constructor, by contrast, is
/// validating the shape of its own single input argument in one pass — the same kind of
/// check <see cref="TopicDescriptor"/>'s own constructor already performs on
/// <c>topicId</c> (<see cref="ArgumentException"/> for an invalid ID, not
/// <see cref="InvalidOperationException"/>). An invalid-shaped constructor argument is an
/// <see cref="ArgumentException"/>, matching that closer precedent.
/// </para>
/// <para><b>No premature instantiation.</b></para>
/// <para>
/// Every member of this type reads only <see cref="TopicDescriptor.TopicId"/> (for
/// dictionary keys and duplicate detection) and returns descriptor references unchanged.
/// <see cref="TopicDescriptor.Factory"/> is never read as a delegate target and never
/// invoked, anywhere in this type — construction, <see cref="Contains"/>,
/// <see cref="TryGetDescriptor"/>, and <see cref="Descriptors"/> enumeration are all pure
/// data operations. See <c>ConversaCore.Tests.Runtime.TopicCatalogTests</c> for the
/// throw-on-construct probe technique (matching CC-104's
/// <c>TopicRegistrationInstanceSeparationTests</c>) that proves this holds.
/// </para>
/// <para><b>Not wired into DI here.</b></para>
/// <para>
/// This ticket (CC-202) implements the type only. Registering
/// <c>services.AddSingleton&lt;ITopicCatalog&gt;(sp =&gt; new TopicCatalog(sp.GetServices&lt;TopicDescriptor&gt;()))</c>
/// (or equivalent) inside <see cref="ConversaCoreBuilder"/>/<c>AddConversaCore</c> is
/// explicitly deferred to a later integration ticket, per this ticket's constraints.
/// </para>
/// </remarks>
public sealed class TopicCatalog : ITopicCatalog
{
    private readonly Dictionary<string, TopicDescriptor> _byId;
    private readonly TopicDescriptor[] _descriptors;

    /// <summary>
    /// Creates a new immutable catalog from a resolved set of topic descriptors, typically
    /// obtained the way CC-104's acceptance evidence anticipates —
    /// <c>IServiceProvider.GetServices&lt;TopicDescriptor&gt;()</c> (equivalently,
    /// constructor injection of <c>IEnumerable&lt;TopicDescriptor&gt;</c>) over descriptors
    /// registered through <see cref="ConversaCoreBuilder"/>'s <c>AddTopic</c> family — but
    /// accepting any <see cref="IEnumerable{T}"/> of <see cref="TopicDescriptor"/>, since
    /// nothing about this type depends on how its input was produced.
    /// </summary>
    /// <param name="descriptors">
    /// The full set of descriptors this catalog should hold. Enumerated exactly once and
    /// defensively copied — see this type's remarks for the immutability and duplicate-ID
    /// handling this implies. Must not be null. May be empty (an empty catalog is valid).
    /// Must not contain two descriptors whose <see cref="TopicDescriptor.TopicId"/> values
    /// are equal (case-insensitive, per <see cref="TopicDescriptor"/>'s own equality); must
    /// not contain a null element.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptors"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="descriptors"/> contains a null element, or contains two
    /// or more descriptors sharing the same <see cref="TopicDescriptor.TopicId"/>
    /// (case-insensitive).
    /// </exception>
    public TopicCatalog(IEnumerable<TopicDescriptor> descriptors)
    {
        if (descriptors is null)
            throw new ArgumentNullException(nameof(descriptors));

        var snapshot = descriptors as IReadOnlyList<TopicDescriptor> ?? descriptors.ToList();

        var byId = new Dictionary<string, TopicDescriptor>(snapshot.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in snapshot)
        {
            if (descriptor is null)
            {
                throw new ArgumentException(
                    "The descriptor collection must not contain a null element.", nameof(descriptors));
            }

            if (!byId.TryAdd(descriptor.TopicId, descriptor))
            {
                throw new ArgumentException(
                    $"Topic ID '{descriptor.TopicId}' appears more than once in the supplied descriptor set. " +
                    "Topic IDs must be unique (case-insensitive); TopicCatalog cannot resolve an ambiguous " +
                    "topic identity by silently picking a winner.",
                    nameof(descriptors));
            }
        }

        _byId = byId;

        // Always copy into a new array, even when the caller already supplied one — this
        // catalog must not hold a reference to the caller's original storage, or a later
        // in-place element replacement in that array (e.g. myArray[0] = otherDescriptor)
        // would silently change this "immutable" catalog's contents.
        var ownedCopy = new TopicDescriptor[snapshot.Count];
        for (var i = 0; i < snapshot.Count; i++)
        {
            ownedCopy[i] = snapshot[i];
        }

        _descriptors = ownedCopy;
    }

    /// <inheritdoc />
    public int Count => _byId.Count;

    /// <inheritdoc />
    public IReadOnlyCollection<TopicDescriptor> Descriptors => _descriptors;

    /// <inheritdoc />
    public bool Contains(string topicId)
    {
        ValidateTopicId(topicId);
        return _byId.ContainsKey(topicId);
    }

    /// <inheritdoc />
    public bool TryGetDescriptor(string topicId, out TopicDescriptor? descriptor)
    {
        ValidateTopicId(topicId);
        return _byId.TryGetValue(topicId, out descriptor);
    }

    private static void ValidateTopicId(string topicId)
    {
        if (string.IsNullOrWhiteSpace(topicId))
        {
            throw new ArgumentException("Topic ID must not be null, empty, or whitespace.", nameof(topicId));
        }
    }
}
