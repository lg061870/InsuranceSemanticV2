using ConversaCore.Runtime;
using Microsoft.Extensions.Logging;

namespace ConversaCore.UI.Lifecycle;

/// <summary>
/// Owns the single output subscription created by a chat component for its circuit.
/// </summary>
internal sealed class ConversationOutputSubscriptionOwner : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IConversationRuntime _runtime;
    private readonly Func<ConversationOutput, CancellationToken, Task> _handleOutputAsync;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private Task? _startTask;
    private Task? _disposeTask;
    private IConversationOutputSubscription? _subscription;
    private Task? _pumpTask;
    private bool _disposeRequested;

    public ConversationOutputSubscriptionOwner(
        IConversationRuntime runtime,
        Func<ConversationOutput, CancellationToken, Task> handleOutputAsync,
        ILogger logger)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _handleOutputAsync = handleOutputAsync ?? throw new ArgumentNullException(nameof(handleOutputAsync));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Creates the subscription and starts the runtime exactly once.</summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposeRequested, this);
            return _startTask ??= StartCoreAsync(cancellationToken);
        }
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        _subscription = _runtime.Subscribe();
        _pumpTask = PumpAsync(_subscription, _lifetimeCancellation.Token);

        using var startCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token);
        await _runtime.StartAsync(startCancellation.Token).ConfigureAwait(false);
    }

    private async Task PumpAsync(
        IConversationOutputSubscription subscription,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var output in subscription.ReadAllAsync(cancellationToken)
                               .ConfigureAwait(false))
            {
                try
                {
                    await _handleOutputAsync(output, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Conversation output handler failed for {OutputType}; subscription remains active",
                        output.GetType().Name);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Conversation output subscription cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Conversation output subscription failed");
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _disposeRequested = true;
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }
    }

    private async Task DisposeCoreAsync()
    {
        _lifetimeCancellation.Cancel();
        Exception? disposalFailure = null;

        try
        {
            if (_startTask is not null)
                await _startTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Expected when component disposal interrupts runtime startup.
        }
        catch (Exception exception)
        {
            disposalFailure = exception;
        }

        try
        {
            if (_subscription is not null)
                await _subscription.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            disposalFailure = disposalFailure is null
                ? exception
                : new AggregateException(disposalFailure, exception);
            _logger.LogError(exception, "Conversation output subscription disposal failed");
        }

        try
        {
            if (_pumpTask is not null)
                await _pumpTask.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            disposalFailure = disposalFailure is null
                ? exception
                : new AggregateException(disposalFailure, exception);
        }
        finally
        {
            _lifetimeCancellation.Dispose();
        }

        if (disposalFailure is not null)
            throw disposalFailure;
    }
}
