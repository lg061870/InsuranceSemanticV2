using ConversaCore.Topics;

namespace ConversaCore.Runtime;

/// <summary>
/// Combines an immutable <see cref="TopicDescriptor"/> (looked up in an
/// <see cref="ITopicCatalog"/>) with the active conversation's scoped
/// <see cref="IServiceProvider"/> to produce a live, mutable <see cref="ITopic"/>
/// instance, ready to route input to. This is CC-203's implementation of target
/// architecture section 7.1's "future scoped activation mechanism (an
/// <c>ITopicActivator</c>) combines a descriptor with the active conversation scope to
/// produce a live, mutable topic instance," and of the work breakdown's CC-203 task text:
/// "Resolve or construct the topic instance from the active conversation scope and await
/// initialization before exposing it."
/// </summary>
/// <remarks>
/// <para><b>What activation does, concretely.</b></para>
/// <para>
/// <see cref="ActivateAsync"/> performs three steps: (1) look up
/// <paramref name="topicId"/> ... see the method's own remarks and
/// <see cref="TopicActivator"/> remarks for the full behavior, including the
/// <see cref="IAsyncInitializable"/> seam this ticket defines for step (3) ("await
/// initialization").
/// </para>
/// <para><b>Scope of this ticket (CC-203) — what this is not.</b></para>
/// <list type="bullet">
/// <item><description>
/// <b>Not <c>ITopicRouter</c> (CC-204).</b> This type does not decide <i>which</i> topic
/// to activate — it activates a specific, already-chosen <paramref name="topicId"/>. A
/// future router selects the ID; this activator turns that decision into a live instance.
/// </description></item>
/// <item><description>
/// <b>Not <c>IWorkflowRunner</c> (CC-205).</b> This type returns a ready-to-run
/// <see cref="ITopic"/>; it does not run it, does not execute any activity, and does not
/// record the returned instance anywhere. In particular it does not call
/// <see cref="IConversationSession.SetActiveTopic"/> — composing activation with marking a
/// topic "active" on the session is a caller's job (eventually the router/runner), not
/// this ticket's.
/// </description></item>
/// <item><description>
/// <b>Not CC-209.</b> This ticket defines the awaitable initialization seam
/// (<see cref="IAsyncInitializable"/>) and proves the activator uses it correctly; it does
/// not migrate any real topic's existing constructor-background-work anti-pattern onto
/// that seam. See <see cref="IAsyncInitializable"/> remarks.
/// </description></item>
/// <item><description>
/// <b>Not DI wiring.</b> Registering an <see cref="ITopicActivator"/> implementation into
/// <c>ConversaCoreBuilder</c>/<c>AddConversaCore</c> is a later integration ticket, per
/// this ticket's constraints — the same deferral <see cref="TopicCatalog"/> (CC-202) and
/// <see cref="ConversationSession"/> (CC-201) already documented for themselves.
/// </description></item>
/// </list>
/// </remarks>
public interface ITopicActivator
{
    /// <summary>
    /// Activates the topic registered under <paramref name="topicId"/>: looks it up in
    /// this activator's <see cref="ITopicCatalog"/>, invokes its
    /// <see cref="TopicDescriptor.Factory"/> with <paramref name="serviceProvider"/> to
    /// resolve or construct a fresh <see cref="ITopic"/> instance, awaits that instance's
    /// asynchronous initialization when it implements <see cref="IAsyncInitializable"/>,
    /// and returns the fully-initialized instance. The returned instance is never handed
    /// to a caller while its initialization is still in flight — activation cannot race
    /// initialization, per target architecture section 7.3.
    /// </summary>
    /// <param name="topicId">
    /// The stable <see cref="TopicDescriptor.TopicId"/> of the topic to activate. Must not
    /// be null, empty, or whitespace. Compared case-insensitively (ordinal), matching
    /// <see cref="ITopicCatalog"/>'s own lookup semantics.
    /// </param>
    /// <param name="serviceProvider">
    /// The service provider scoped to the active conversation. Passed directly to
    /// <see cref="TopicDescriptor.Factory"/> unchanged — this activator does not create,
    /// own, or dispose any scope; the caller is responsible for supplying a provider
    /// scoped appropriately for one conversation. Must not be null.
    /// </param>
    /// <param name="cancellationToken">
    /// A token observed before invoking the factory and passed through to
    /// <see cref="IAsyncInitializable.InitializeAsync"/> when the activated instance
    /// implements that interface.
    /// </param>
    /// <returns>
    /// A task that completes with the fully-activated, fully-initialized
    /// <see cref="ITopic"/> instance the factory produced.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="topicId"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="serviceProvider"/> is null.
    /// </exception>
    /// <exception cref="TopicActivationException">
    /// Thrown when <paramref name="topicId"/> is not registered in this activator's
    /// <see cref="ITopicCatalog"/>, or when the matching
    /// <see cref="TopicDescriptor.Factory"/> returns <see langword="null"/> instead of a
    /// usable <see cref="ITopic"/> instance. Activating a topic ID a caller asserts should
    /// exist is a real error condition, unlike <see cref="ITopicCatalog.TryGetDescriptor"/>'s
    /// "not found is normal" query semantics — see <see cref="TopicActivator"/> remarks.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled before or during
    /// activation.
    /// </exception>
    Task<ITopic> ActivateAsync(
        string topicId,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default);
}
