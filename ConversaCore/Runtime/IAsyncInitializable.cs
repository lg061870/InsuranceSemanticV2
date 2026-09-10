namespace ConversaCore.Runtime;

/// <summary>
/// An optional seam a topic implementation may implement to declare that it needs
/// asynchronous initialization work performed, and awaited, before it is exposed to a
/// caller as ready to route input to. This is the "awaited topic build/activation phase"
/// target architecture section 7.3 describes: "Construction performs no background
/// work. Any asynchronous build or initialization is awaited before a topic becomes
/// routable... Topic activation cannot race its initialization."
/// </summary>
/// <remarks>
/// <para><b>Why this exists: CC-203 needs an awaitable seam, but none exists yet.</b></para>
/// <para>
/// CC-203's job (per the work breakdown) is to "resolve a fresh mutable topic execution
/// from the current conversation scope and await initialization before exposing it." But
/// today, <c>ITopic</c>/<c>ConversaCore.TopicFlow.TopicFlow</c> construction is an
/// ordinary, synchronous .NET constructor — there is no member on the base topic contract
/// an activator can await. The seam replaces constructor-launched work; InsuranceAgent's
/// marketing T1 topic now implements it so activation cannot race its rule selection and
/// workflow construction.
/// </para>
/// <para><b>Why a new interface, not an addition to <see cref="ConversaCore.Topics.ITopic"/>.</b></para>
/// <para>
/// Making initialization mandatory on every topic (for example, by adding
/// <c>InitializeAsync</c> directly to <c>ITopic</c>) would force every existing topic
/// implementation to grow a no-op method for a capability the overwhelming majority of
/// them do not need — <c>ITopic</c>/<c>TopicFlow</c> construction is synchronous today,
/// with exactly one documented constructor-background-work offender in the entire
/// codebase (see above). An optional, separately-implementable interface lets an
/// activator ask "does this specific instance need to be awaited before use?" via a
/// simple type check (<c>topic is IAsyncInitializable initializable</c>), and lets every
/// topic that has no asynchronous initialization need — which is most topics —
/// remain completely unaware this interface exists. This mirrors a common .NET pattern
/// (for example, <c>IAsyncDisposable</c> alongside <c>IDisposable</c>): an optional
/// capability interface a type implements only when it actually needs the capability,
/// discovered by the consumer via a type check rather than required by the base contract.
/// </para>
/// <para><b>Not a re-use of an existing convention.</b></para>
/// <para>
/// No existing async-initialization convention was found to reuse: a repository-wide
/// search for <c>InitializeAsync</c> and <c>IAsyncInitializable</c> across the codebase at
/// the time this ticket was implemented found no matches outside an unrelated Visual
/// Studio extension (<c>ConversaCore.SDK/ConversaCore.TopicDesigner.VSIX</c>), and
/// <c>ITopic</c> (see its remarks) declares no such member. This interface is therefore
/// new, purpose-built seam for this ticket, not an adoption of a pre-existing pattern.
/// </para>
/// </remarks>
public interface IAsyncInitializable
{
    /// <summary>
    /// Performs this instance's asynchronous initialization work. A caller (today, only
    /// <c>TopicActivator</c>) must await the returned <see cref="Task"/> to completion
    /// before treating this instance as ready to route input to. Implementations should
    /// perform no synchronous blocking work here beyond what is unavoidable, should
    /// observe <paramref name="cancellationToken"/> where the underlying work supports
    /// cancellation, and should let a failure surface as a faulted <see cref="Task"/>
    /// (an exception thrown from, or that faults, this method) rather than being silently
    /// swallowed — unlike the untracked <c>_ = Task.Run(...)</c> pattern this seam exists
    /// to replace, whose <c>catch</c> block only logs and never propagates failure to any
    /// caller.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token the implementation should observe to cancel in-flight initialization work
    /// where practical.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that completes when initialization has finished. The instance
    /// must not be treated as routable until this task completes successfully.
    /// </returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
