using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ConversaCore.Runtime;
using ConversaCore.UI.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConversaCore.Tests.UI;

public sealed class ConversationOutputSubscriptionOwnerTests
{
    [Fact]
    public async Task StartAsync_WhenCalledRepeatedly_CreatesAndStartsExactlyOnce()
    {
        var runtime = new RecordingRuntime();
        await using var owner = CreateOwner(runtime, (_, _) => Task.CompletedTask);

        await Task.WhenAll(owner.StartAsync(), owner.StartAsync(), owner.StartAsync());

        Assert.Equal(1, runtime.SubscribeCount);
        Assert.Equal(1, runtime.StartCount);
    }

    [Fact]
    public async Task OutputHandlerFailure_DoesNotTerminateSubscription()
    {
        var runtime = new RecordingRuntime();
        var handled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        await using var owner = CreateOwner(runtime, (output, _) =>
        {
            if (Interlocked.Increment(ref invocationCount) == 1)
                throw new InvalidOperationException("simulated component callback failure");

            handled.TrySetResult();
            return Task.CompletedTask;
        });
        await owner.StartAsync();

        runtime.Subscription.Publish(new MessageOutput(runtime.ConversationId, "first"));
        runtime.Subscription.Publish(new MessageOutput(runtime.ConversationId, "second"));
        await handled.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, invocationCount);
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledRepeatedly_CancelsPumpAndDisposesSubscriptionOnce()
    {
        var runtime = new RecordingRuntime();
        var owner = CreateOwner(runtime, (_, _) => Task.CompletedTask);
        await owner.StartAsync();

        await Task.WhenAll(owner.DisposeAsync().AsTask(), owner.DisposeAsync().AsTask());

        Assert.Equal(1, runtime.Subscription.DisposeCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => owner.StartAsync());
    }

    [Fact]
    public async Task DisposeAsync_WhileRuntimeStartIsPending_CancelsAndAwaitsStartup()
    {
        var runtime = new RecordingRuntime();
        var startEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.StartHandler = async cancellationToken =>
        {
            startEntered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };
        var owner = CreateOwner(runtime, (_, _) => Task.CompletedTask);

        var startTask = owner.StartAsync();
        await startEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await owner.DisposeAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startTask);
        Assert.True(startTask.IsCompleted);
        Assert.Equal(1, runtime.Subscription.DisposeCount);
    }

    private static ConversationOutputSubscriptionOwner CreateOwner(
        IConversationRuntime runtime,
        Func<ConversationOutput, CancellationToken, Task> handler) =>
        new(runtime, handler, NullLogger.Instance);

    private sealed class RecordingRuntime : IConversationRuntime
    {
        public string ConversationId { get; } = "conversation-lifecycle";
        public RecordingSubscription Subscription { get; } = new();
        public int SubscribeCount { get; private set; }
        public int StartCount { get; private set; }
        public Func<CancellationToken, Task>? StartHandler { get; set; }

        public IConversationOutputSubscription Subscribe()
        {
            SubscribeCount++;
            return Subscription;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            return StartHandler?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public Task SendMessageAsync(string message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SubmitCardAsync(CardSubmission submission, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RespondToHostInteractionAsync(
            HostInteractionResponse response,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingSubscription : IConversationOutputSubscription
    {
        private readonly Channel<ConversationOutput> _outputs = Channel.CreateUnbounded<ConversationOutput>();
        public int DisposeCount { get; private set; }

        public void Publish(ConversationOutput output) => _outputs.Writer.TryWrite(output);

        public async IAsyncEnumerable<ConversationOutput> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var output in _outputs.Reader.ReadAllAsync(cancellationToken))
                yield return output;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            _outputs.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
