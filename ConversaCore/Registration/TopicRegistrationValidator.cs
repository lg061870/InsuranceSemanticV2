namespace ConversaCore.Registration;

/// <summary>
/// Aggregated, structural startup validation for a resolved set of
/// <see cref="TopicDescriptor"/> registrations (CC-103, ConversaCore transformation work
/// breakdown WP1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> CC-103's originally listed checks were "duplicate IDs, duplicate
/// aliases, invalid lifetimes, missing fallback/start topics, and unresolved subtopic
/// references." This validator implements the subset that is concretely checkable
/// against the current, already-shipped <see cref="TopicDescriptor"/> (CC-101) shape
/// without inventing new descriptor concepts:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Duplicate topic IDs</b> — an independent, defense-in-depth pass over the full
/// resolved descriptor set. <see cref="ConversaCoreBuilderTopicExtensions.AddTopic(ConversaCoreBuilder, TopicDescriptor)"/>
/// (CC-102) already throws immediately on a duplicate ID registered through the builder,
/// but a descriptor can still be added some other way (for example,
/// <c>services.AddSingleton(descriptor)</c> directly), bypassing that check. This
/// validator re-checks the entire resolved set independently of how each descriptor got
/// there.
/// </description></item>
/// <item><description>
/// <b>Missing start/fallback topics</b> — <see cref="TopicDescriptor"/> has no built-in
/// "this is the start topic" or "this is the fallback topic" marker, and none is added
/// here. Instead, the designated IDs are supplied explicitly as the <c>startTopicId</c>
/// and <c>fallbackTopicId</c> parameters below (matching target architecture section
/// 7.2's "registered system fallback topic"), and this validator confirms a
/// <see cref="TopicDescriptor"/> with each supplied ID actually exists.
/// </description></item>
/// </list>
/// <para>
/// <b>Explicitly deferred.</b> Three of CC-103's originally listed checks are out of
/// scope here, on purpose:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Duplicate aliases</b> — <see cref="TopicDescriptor"/> has no alias concept at all.
/// Adding one is a separate design decision for a future ticket, not something this
/// validator should retrofit onto CC-101's already-shipped, already-tested shape.
/// </description></item>
/// <item><description>
/// <b>Invalid lifetimes</b> — there is no <c>ITopicCatalog</c>/<c>ITopicActivator</c> yet
/// (CC-104/CC-202), so there is no real topic-activation lifetime model to validate
/// against beyond the ordinary DI service-lifetime rules the .NET DI container already
/// enforces on its own. Inventing a lifetime concept here would anticipate a design this
/// ticket should not make unilaterally.
/// </description></item>
/// <item><description>
/// <b>Unresolved subtopic references</b> — <see cref="TopicDescriptor"/> declares no
/// "topics this topic calls as subtopics" field, so there is nothing in the descriptor
/// shape to check a subtopic reference against. Adding such a field is a bigger,
/// separately tracked design decision.
/// </description></item>
/// </list>
/// <para>
/// <b>Cost and side effects.</b> Validation only inspects
/// <see cref="TopicDescriptor.TopicId"/> values; it never invokes
/// <see cref="TopicDescriptor.Factory"/> and therefore never constructs a topic instance.
/// It is structural, fast, and side-effect free, matching target architecture section
/// 7.2's "Topic eligibility checks must be side-effect free and fast."
/// </para>
/// </remarks>
public static class TopicRegistrationValidator
{
    /// <summary>
    /// Validates a resolved set of <see cref="TopicDescriptor"/>s, aggregating every
    /// problem found — every duplicate topic ID, plus a missing start topic and/or a
    /// missing fallback topic when those IDs are supplied — into one
    /// <see cref="TopicRegistrationValidationResult"/> instead of stopping at the first
    /// problem.
    /// </summary>
    /// <param name="descriptors">
    /// The full resolved set of registered descriptors to validate, typically obtained
    /// from <c>IServiceProvider.GetServices&lt;TopicDescriptor&gt;()</c> (or
    /// <c>IServiceCollection</c> resolved into a temporary provider) so that descriptors
    /// registered by any means — not only through <see cref="ConversaCoreBuilderTopicExtensions.AddTopic(ConversaCoreBuilder, TopicDescriptor)"/> —
    /// are covered. Enumerated exactly once.
    /// </param>
    /// <param name="startTopicId">
    /// The topic ID the host designates as the conversation's start topic, if it has one.
    /// When non-null, a <see cref="TopicDescriptor"/> with this <see cref="TopicDescriptor.TopicId"/>
    /// (case-insensitive) must exist in <paramref name="descriptors"/> or a
    /// <see cref="TopicRegistrationValidationErrorCode.MissingStartTopic"/> problem is
    /// reported. Pass <see langword="null"/> (the default) when the host has no start
    /// topic to validate yet, or does not want this check performed.
    /// </param>
    /// <param name="fallbackTopicId">
    /// The topic ID the host designates as the registered system fallback topic (target
    /// architecture section 7.2), if it has one. Validated the same way as
    /// <paramref name="startTopicId"/>, reporting
    /// <see cref="TopicRegistrationValidationErrorCode.MissingFallbackTopic"/> when
    /// absent. Pass <see langword="null"/> (the default) when the host has no fallback
    /// topic yet, or does not want this check performed.
    /// </param>
    /// <returns>
    /// A <see cref="TopicRegistrationValidationResult"/> whose <see cref="TopicRegistrationValidationResult.Errors"/>
    /// contains every problem found (possibly empty, meaning validation passed).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="descriptors"/> is null.</exception>
    public static TopicRegistrationValidationResult Validate(
        IEnumerable<TopicDescriptor> descriptors,
        string? startTopicId = null,
        string? fallbackTopicId = null)
    {
        if (descriptors is null)
            throw new ArgumentNullException(nameof(descriptors));

        var descriptorList = descriptors as IReadOnlyList<TopicDescriptor> ?? descriptors.ToList();
        var errors = new List<TopicRegistrationValidationError>();

        AppendDuplicateIdErrors(descriptorList, errors);
        AppendMissingDesignatedTopicError(
            descriptorList, startTopicId, "start", TopicRegistrationValidationErrorCode.MissingStartTopic, errors);
        AppendMissingDesignatedTopicError(
            descriptorList, fallbackTopicId, "fallback", TopicRegistrationValidationErrorCode.MissingFallbackTopic, errors);

        return errors.Count == 0
            ? TopicRegistrationValidationResult.Success
            : new TopicRegistrationValidationResult(errors);
    }

    /// <summary>
    /// Groups <paramref name="descriptors"/> by <see cref="TopicDescriptor.TopicId"/>
    /// (case-insensitive, matching <see cref="TopicDescriptor"/>'s own equality) and
    /// appends one <see cref="TopicRegistrationValidationErrorCode.DuplicateTopicId"/>
    /// error per colliding ID — one error per ID regardless of how many extra copies
    /// exist, not one error per extra copy.
    /// </summary>
    private static void AppendDuplicateIdErrors(
        IReadOnlyList<TopicDescriptor> descriptors,
        List<TopicRegistrationValidationError> errors)
    {
        var duplicateGroups = descriptors
            .GroupBy(descriptor => descriptor.TopicId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            errors.Add(new TopicRegistrationValidationError(
                TopicRegistrationValidationErrorCode.DuplicateTopicId,
                $"Topic ID '{group.Key}' is registered {group.Count()} times. " +
                "Topic IDs must be unique (case-insensitive); remove or rename the duplicate registration."));
        }
    }

    /// <summary>
    /// Appends an error when <paramref name="designatedTopicId"/> is non-null but no
    /// descriptor in <paramref name="descriptors"/> has a matching
    /// <see cref="TopicDescriptor.TopicId"/> (case-insensitive). Does nothing when
    /// <paramref name="designatedTopicId"/> is null — the host has no topic designated
    /// for that role, or does not want it validated.
    /// </summary>
    private static void AppendMissingDesignatedTopicError(
        IReadOnlyList<TopicDescriptor> descriptors,
        string? designatedTopicId,
        string roleDescription,
        TopicRegistrationValidationErrorCode errorCode,
        List<TopicRegistrationValidationError> errors)
    {
        if (designatedTopicId is null)
            return;

        var exists = descriptors.Any(descriptor =>
            string.Equals(descriptor.TopicId, designatedTopicId, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            errors.Add(new TopicRegistrationValidationError(
                errorCode,
                $"The designated {roleDescription} topic ID '{designatedTopicId}' has no matching registered TopicDescriptor."));
        }
    }
}
