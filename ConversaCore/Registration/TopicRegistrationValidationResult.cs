namespace ConversaCore.Registration;

/// <summary>
/// Identifies the kind of problem a <see cref="TopicRegistrationValidationError"/>
/// reports, so callers can react to specific problem categories programmatically instead
/// of parsing <see cref="TopicRegistrationValidationError.Message"/> text.
/// </summary>
/// <remarks>
/// This is the concrete, currently-implementable subset of CC-103's originally listed
/// checks (see the ConversaCore transformation work breakdown, WP1, CC-103). Duplicate
/// topic aliases, invalid topic lifetimes, and unresolved subtopic references are
/// explicitly deferred — <see cref="TopicDescriptor"/> declares no alias, lifetime, or
/// subtopic-reference concept for this validator to check today, and adding one is a
/// bigger, separately tracked design decision this ticket does not make unilaterally.
/// </remarks>
public enum TopicRegistrationValidationErrorCode
{
    /// <summary>
    /// Two or more registered <see cref="TopicDescriptor"/>s share the same
    /// <see cref="TopicDescriptor.TopicId"/> (case-insensitive). This is an independent,
    /// defense-in-depth pass over the full resolved descriptor set: it catches a
    /// duplicate even when it was registered in a way that bypasses
    /// <see cref="ConversaCoreBuilderTopicExtensions.AddTopic(ConversaCoreBuilder, TopicDescriptor)"/>'s
    /// own immediate, single-registration duplicate check (for example, a descriptor
    /// added directly via <c>services.AddSingleton(descriptor)</c>).
    /// </summary>
    DuplicateTopicId,

    /// <summary>
    /// The caller designated a start-topic ID (see
    /// <see cref="TopicRegistrationValidator.Validate(System.Collections.Generic.IEnumerable{TopicDescriptor}, string?, string?)"/>'s
    /// <c>startTopicId</c> parameter) that does not match any registered
    /// <see cref="TopicDescriptor.TopicId"/>.
    /// </summary>
    MissingStartTopic,

    /// <summary>
    /// The caller designated a fallback-topic ID (see
    /// <see cref="TopicRegistrationValidator.Validate(System.Collections.Generic.IEnumerable{TopicDescriptor}, string?, string?)"/>'s
    /// <c>fallbackTopicId</c> parameter, matching target architecture section 7.2's
    /// "registered system fallback topic") that does not match any registered
    /// <see cref="TopicDescriptor.TopicId"/>.
    /// </summary>
    MissingFallbackTopic
}

/// <summary>
/// One problem found by <see cref="TopicRegistrationValidator"/>: a stable
/// <see cref="Code"/> for programmatic handling plus a human-readable
/// <see cref="Message"/> for diagnostics and logs.
/// </summary>
/// <param name="Code">The category of problem this error reports.</param>
/// <param name="Message">A human-readable description of the specific problem instance.</param>
public sealed record TopicRegistrationValidationError(TopicRegistrationValidationErrorCode Code, string Message)
{
    /// <summary>Returns <see cref="Message"/>, so this error reads naturally in aggregated diagnostic text.</summary>
    public override string ToString() => Message;
}

/// <summary>
/// The aggregated outcome of one <see cref="TopicRegistrationValidator"/> validation pass:
/// every problem found, collected together, rather than only the first one encountered.
/// </summary>
/// <remarks>
/// Per the ConversaCore transformation work breakdown's CC-103 scope ("Startup
/// validation should ... report every duplicate ID found ... this validation should be
/// aggregated across all problems"), a caller runs one validation pass and receives one
/// report covering every duplicate topic ID and every missing designated start/fallback
/// topic, instead of the process stopping at the first problem found.
/// </remarks>
public sealed class TopicRegistrationValidationResult
{
    /// <summary>A reusable, allocation-free result representing "no problems found."</summary>
    public static TopicRegistrationValidationResult Success { get; } = new(Array.Empty<TopicRegistrationValidationError>());

    /// <summary>Every problem found during validation, in the order discovered. Empty when <see cref="IsValid"/> is <see langword="true"/>.</summary>
    public IReadOnlyList<TopicRegistrationValidationError> Errors { get; }

    /// <summary><see langword="true"/> when <see cref="Errors"/> is empty; otherwise <see langword="false"/>.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Creates a result carrying the given set of problems (which may be empty).
    /// </summary>
    /// <param name="errors">Every problem found during validation. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    public TopicRegistrationValidationResult(IReadOnlyList<TopicRegistrationValidationError> errors)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    /// <summary>
    /// Throws a single <see cref="TopicRegistrationValidationException"/> listing every
    /// problem in <see cref="Errors"/> when <see cref="IsValid"/> is <see langword="false"/>;
    /// otherwise does nothing. This is the "fail on the aggregated report" half of CC-103 —
    /// a caller that wants startup to hard-fail on any problem calls this once validation
    /// has finished collecting every problem, rather than throwing from inside the
    /// validation pass itself.
    /// </summary>
    /// <exception cref="TopicRegistrationValidationException">Thrown when <see cref="IsValid"/> is <see langword="false"/>.</exception>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
            throw new TopicRegistrationValidationException(this);
    }
}
