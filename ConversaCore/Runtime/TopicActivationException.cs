namespace ConversaCore.Runtime;

/// <summary>
/// Thrown by <see cref="ITopicActivator.ActivateAsync"/> when a topic activation request
/// cannot be satisfied — either because the requested <see cref="TopicId"/> is not
/// registered in the <see cref="ITopicCatalog"/> the activator consults, or because the
/// matching <see cref="ConversaCore.Registration.TopicDescriptor.Factory"/> did not
/// produce a usable topic instance. See <see cref="TopicActivator"/> remarks for why an
/// unregistered ID is treated as an error here, in contrast to
/// <see cref="ITopicCatalog.TryGetDescriptor"/>'s "not found is normal" stance.
/// </summary>
/// <remarks>
/// This is a dedicated <see cref="InvalidOperationException"/> subclass — the same choice
/// <see cref="ConversaCore.Registration.TopicRegistrationValidationException"/> makes —
/// rather than a bare <see cref="InvalidOperationException"/>, so a caller that wants to
/// distinguish "this specific topic ID could not be activated" from other invalid-state
/// failures can catch it specifically, and so <see cref="TopicId"/> is available
/// programmatically rather than only embedded in a message string.
/// </remarks>
public sealed class TopicActivationException : InvalidOperationException
{
    /// <summary>The topic ID the caller attempted to activate.</summary>
    public string TopicId { get; }

    /// <summary>
    /// Creates a new activation exception for <paramref name="topicId"/>.
    /// </summary>
    /// <param name="topicId">The topic ID that could not be activated. Must not be null.</param>
    /// <param name="message">A message describing why activation failed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="topicId"/> is null.</exception>
    public TopicActivationException(string topicId, string message)
        : base(message)
    {
        TopicId = topicId ?? throw new ArgumentNullException(nameof(topicId));
    }
}
