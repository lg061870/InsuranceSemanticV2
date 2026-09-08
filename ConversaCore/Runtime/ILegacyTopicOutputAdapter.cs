using ConversaCore.Registration;
using ConversaCore.Topics;

namespace ConversaCore.Runtime;

/// <summary>Compatibility boundary that snapshots legacy topic events as typed outputs.</summary>
/// <remarks>The caller owns the returned lease and must dispose it when the activation ends.
/// New framework code should emit <see cref="ConversationOutput"/> directly.</remarks>
public interface ILegacyTopicOutputAdapter
{
    /// <summary>Attaches output translation to one activated topic.</summary>
    /// <param name="descriptor">Immutable metadata for the activation.</param>
    /// <param name="topic">The activated topic instance.</param>
    /// <returns>An async-disposable lease owning every compatibility subscription.</returns>
    IAsyncDisposable Attach(TopicDescriptor descriptor, ITopic topic);
}
