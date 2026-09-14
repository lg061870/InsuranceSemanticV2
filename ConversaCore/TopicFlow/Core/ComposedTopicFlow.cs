using ConversaCore.Runtime;
using ConversaCore.Models;
using Microsoft.Extensions.Logging;

namespace ConversaCore.TopicFlow;

/// <summary>
/// A <see cref="TopicFlow"/> whose activity graph is composed after dependency injection
/// has completely constructed the derived topic.
/// </summary>
/// <remarks>
/// <para>
/// Derive generated and newly authored topics from this type and implement
/// <see cref="ComposeWorkflow"/>. The base constructor never invokes that override.
/// <see cref="TopicActivator"/> observes <see cref="IAsyncInitializable"/> and calls
/// <see cref="InitializeAsync"/> only after the derived constructor has returned.
/// </para>
/// <para>
/// Composition is synchronous because it creates an in-memory activity graph. Activities
/// perform asynchronous work when the workflow runner executes them. A topic that must load
/// external data before it can compose should use a separately designed asynchronous
/// initialization contract rather than blocking here or launching background constructor work.
/// </para>
/// <para>
/// Existing constructor-composed <see cref="TopicFlow"/> implementations remain compatible;
/// this opt-in base is the safe authoring path for generated source and migrated topics.
/// </para>
/// </remarks>
public abstract class ComposedTopicFlow : TopicFlow, IAsyncInitializable
{
    private readonly object _compositionGate = new();
    private bool _isComposed;
    private bool _isComposing;

    /// <summary>Initializes a new composed topic without invoking derived composition code.</summary>
    /// <param name="context">Conversation-scoped workflow state.</param>
    /// <param name="logger">Logger used by the base workflow.</param>
    /// <param name="name">Stable diagnostic topic name.</param>
    protected ComposedTopicFlow(TopicWorkflowContext context, ILogger logger, string name = "TopicFlow")
        : base(context, logger, name)
    {
    }

    /// <summary>
    /// Adds this topic's activities in execution order. Called only after the derived
    /// constructor has completed, and again after a successful reset.
    /// </summary>
    protected abstract void ComposeWorkflow();

    /// <summary>
    /// Composes the workflow exactly once for this activation. Failed or canceled attempts
    /// leave no partial activity graph and may be retried.
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled(cancellationToken);

        try
        {
            EnsureComposed(cancellationToken);
            return Task.CompletedTask;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    /// <inheritdoc />
    public override async Task<TopicResult> RunAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        return await base.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the base workflow state and rebuilds the activity graph through the same
    /// post-construction composition hook.
    /// </summary>
    public override void Reset()
    {
        lock (_compositionGate)
        {
            if (_isComposing)
                throw new InvalidOperationException($"Cannot reset topic '{Name}' while its workflow is being composed.");

            base.Reset();
            if (IsTerminated)
                return;

            _isComposed = false;
            ComposeCore(CancellationToken.None);
        }
    }

    private void EnsureComposed(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_compositionGate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_isComposed)
                return;

            if (IsTerminated)
                throw new InvalidOperationException($"Cannot compose terminated topic '{Name}'.");

            if (_isComposing)
                throw new InvalidOperationException($"Recursive workflow composition is not allowed for topic '{Name}'.");

            ComposeCore(cancellationToken);
        }
    }

    private void ComposeCore(CancellationToken cancellationToken)
    {
        _isComposing = true;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ComposeWorkflow();
            cancellationToken.ThrowIfCancellationRequested();
            _isComposed = true;
        }
        catch
        {
            ClearActivities();
            _isComposed = false;
            throw;
        }
        finally
        {
            _isComposing = false;
        }
    }
}
