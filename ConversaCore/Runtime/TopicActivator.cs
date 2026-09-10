using ConversaCore.Registration;
using ConversaCore.Topics;

namespace ConversaCore.Runtime;

/// <summary>
/// Default implementation of <see cref="ITopicActivator"/> (CC-203). Looks a
/// <see cref="TopicDescriptor"/> up in a supplied <see cref="ITopicCatalog"/>, invokes its
/// <see cref="TopicDescriptor.Factory"/> against the caller's conversation-scoped
/// <see cref="IServiceProvider"/>, and awaits the resulting instance's
/// <see cref="IAsyncInitializable.InitializeAsync"/> when it implements that optional
/// seam. See <see cref="ITopicActivator"/> remarks for this type's role and explicit scope
/// boundary against <c>ITopicRouter</c> (CC-204), <c>IWorkflowRunner</c> (CC-205), and
/// CC-209.
/// </summary>
/// <remarks>
/// <para><b>Unregistered topic ID: an error, not a normal "not found."</b></para>
/// <para>
/// <see cref="ITopicCatalog.TryGetDescriptor"/> treats an unrecognized ID as a normal,
/// expected outcome of a lookup — "does this topic exist" is a legitimate question a
/// caller (for example, a future router evaluating many candidate IDs) can ask without it
/// being an error. Activation is different: a caller invoking
/// <see cref="ActivateAsync"/> with a specific <paramref name="topicId"/> is not asking
/// "does this exist," it is asserting "this exists — build it." An unregistered ID at
/// that point is a programming or configuration defect (a typo'd ID, a topic that was
/// referenced — for example, as a subtopic — but never registered, or a stale ID left
/// over from a rename), not a normal branch of activation logic a caller should be
/// expected to handle by checking a boolean return first. This activator therefore throws
/// <see cref="TopicActivationException"/> rather than returning a nullable/failure result,
/// matching how <see cref="TopicDescriptor"/>'s own constructor and
/// <see cref="TopicCatalog"/>'s own constructor already treat structurally invalid input
/// as throw-worthy rather than something to signal through a return value.
/// </para>
/// <para><b>The <see cref="IAsyncInitializable"/> seam: check, don't require.</b></para>
/// <para>
/// After the factory produces an instance, this activator performs a single type check —
/// <c>topic is IAsyncInitializable initializable</c> — and awaits
/// <see cref="IAsyncInitializable.InitializeAsync"/> only when that check succeeds. A
/// topic that does not implement <see cref="IAsyncInitializable"/> activates immediately
/// once the factory returns, with no additional await, reflection, or special-casing.
/// InsuranceAgent's marketing T1 topic is the first production consumer of this seam;
/// activator tests cover successful, failed, and canceled initialization independently of
/// that reference application.
/// </para>
/// <para><b>Cancellation.</b></para>
/// <para>
/// <paramref name="cancellationToken"/> (on <see cref="ActivateAsync"/>) is checked once,
/// before invoking <see cref="TopicDescriptor.Factory"/> — the factory itself is a
/// synchronous <see cref="Func{T,TResult}"/> with no cancellation-aware overload, so
/// there is no in-flight factory work to cancel — and is then passed through unchanged to
/// <see cref="IAsyncInitializable.InitializeAsync"/> when that seam is exercised, so a
/// canceled token stops in-flight asynchronous initialization exactly where target
/// architecture section 7.3 says cancellation must flow: "through every topic, activity,
/// semantic call, host interaction, and tool call."
/// </para>
/// <para><b>Not wired into DI here.</b></para>
/// <para>
/// This ticket (CC-203) implements the type only. Registering an
/// <see cref="ITopicActivator"/> implementation into <c>ConversaCoreBuilder</c>/
/// <c>AddConversaCore</c> is explicitly deferred to a later integration ticket, per this
/// ticket's constraints — the same deferral <see cref="TopicCatalog"/> (CC-202) already
/// documented for itself.
/// </para>
/// </remarks>
public sealed class TopicActivator : ITopicActivator
{
    private readonly ITopicCatalog _catalog;

    /// <summary>
    /// Creates a new activator backed by <paramref name="catalog"/>.
    /// </summary>
    /// <param name="catalog">
    /// The topic catalog this activator looks descriptors up in. Must not be null.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
    public TopicActivator(ITopicCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <inheritdoc />
    public async Task<ITopic> ActivateAsync(
        string topicId,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicId))
            throw new ArgumentException("Topic ID must not be null, empty, or whitespace.", nameof(topicId));

        if (serviceProvider is null)
            throw new ArgumentNullException(nameof(serviceProvider));

        cancellationToken.ThrowIfCancellationRequested();

        if (!_catalog.TryGetDescriptor(topicId, out var descriptor) || descriptor is null)
        {
            throw new TopicActivationException(
                topicId,
                $"Cannot activate topic '{topicId}': no topic with that ID is registered in the " +
                "topic catalog. Activating a topic asserts it should exist; if this ID is expected " +
                "to be reachable (for example, as a subtopic reference), confirm it was registered " +
                "with ConversaCoreBuilder.AddTopic(...) under this exact ID.");
        }

        var topic = descriptor.Factory(serviceProvider);

        if (topic is null)
        {
            throw new TopicActivationException(
                topicId,
                $"Cannot activate topic '{topicId}': its registered factory returned null instead " +
                "of a usable ITopic instance.");
        }

        if (topic is IAsyncInitializable initializable)
        {
            await initializable.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        return topic;
    }
}
