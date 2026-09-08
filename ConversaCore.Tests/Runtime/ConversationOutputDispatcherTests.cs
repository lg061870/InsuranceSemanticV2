using ConversaCore.Context;
using ConversaCore.Runtime;

namespace ConversaCore.Tests.Runtime;

/// <summary>Behavioral coverage for the CC-301 ordered output dispatcher.</summary>
public sealed class ConversationOutputDispatcherTests
{
    [Fact]
    public async Task SequentialDispatch_PreservesOrderForEverySubscriber()
    {
        await using var dispatcher = CreateDispatcher();
        var first = dispatcher.Subscribe();
        var second = dispatcher.Subscribe();
        var outputs = Enumerable.Range(1, 5)
            .Select(number => (ConversationOutput)new MessageOutput("conversation-1", $"message-{number}"))
            .ToArray();

        foreach (var output in outputs) await dispatcher.DispatchAsync(output);
        await dispatcher.DisposeAsync();

        Assert.Equal(outputs, await ReadAllAsync(first));
        Assert.Equal(outputs, await ReadAllAsync(second));
    }

    [Fact]
    public async Task ConcurrentDispatch_GivesAllSubscribersTheSameCompleteOrder()
    {
        await using var dispatcher = CreateDispatcher();
        var first = dispatcher.Subscribe();
        var second = dispatcher.Subscribe();
        var publications = Enumerable.Range(1, 100)
            .Select(number => dispatcher.DispatchAsync(new MessageOutput("conversation-1", $"message-{number}")))
            .ToArray();

        await Task.WhenAll(publications);
        await dispatcher.DisposeAsync();
        var firstOutputs = await ReadAllAsync(first);
        var secondOutputs = await ReadAllAsync(second);

        Assert.Equal(100, firstOutputs.Count);
        Assert.Equal(firstOutputs, secondOutputs);
        Assert.Equal(100, firstOutputs.Cast<MessageOutput>().Select(output => output.Message).Distinct().Count());
    }

    [Fact]
    public async Task DisposingOneSubscription_DoesNotAffectAnotherSubscriber()
    {
        await using var dispatcher = CreateDispatcher();
        var disposed = dispatcher.Subscribe();
        var active = dispatcher.Subscribe();

        await dispatcher.DispatchAsync(new MessageOutput("conversation-1", "before"));
        await disposed.DisposeAsync();
        await dispatcher.DispatchAsync(new MessageOutput("conversation-1", "after"));
        await dispatcher.DisposeAsync();

        Assert.Equal(["before", "after"],
            (await ReadAllAsync(active)).Cast<MessageOutput>().Select(output => output.Message));
    }

    [Fact]
    public async Task CancelledEnumeration_ReleasesOnlyThatSubscription()
    {
        await using var dispatcher = CreateDispatcher();
        var cancelled = dispatcher.Subscribe();
        var active = dispatcher.Subscribe();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ReadAllAsync(cancelled, cancellation.Token));
        await dispatcher.DispatchAsync(new MessageOutput("conversation-1", "still delivered"));
        await dispatcher.DisposeAsync();

        var output = Assert.Single(await ReadAllAsync(active));
        Assert.Equal("still delivered", Assert.IsType<MessageOutput>(output).Message);
    }

    [Fact]
    public async Task ConsumerFailure_DoesNotInterruptPublicationOrOtherSubscriber()
    {
        await using var dispatcher = CreateDispatcher();
        var failing = dispatcher.Subscribe();
        var active = dispatcher.Subscribe();
        var consumerFailure = Task.Run(async () =>
        {
            await foreach (var _ in failing.ReadAllAsync())
                throw new ApplicationException("consumer failed");
        });

        await dispatcher.DispatchAsync(new MessageOutput("conversation-1", "first"));
        await Assert.ThrowsAsync<ApplicationException>(() => consumerFailure);
        await dispatcher.DispatchAsync(new MessageOutput("conversation-1", "second"));
        await dispatcher.DisposeAsync();

        Assert.Equal(["first", "second"],
            (await ReadAllAsync(active)).Cast<MessageOutput>().Select(output => output.Message));
    }

    [Fact]
    public async Task Dispatch_RejectsCancellationConversationMismatchAndDisposedDispatcher()
    {
        var dispatcher = CreateDispatcher();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync(new MessageOutput("conversation-1", "cancelled"), cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            dispatcher.DispatchAsync(new MessageOutput("conversation-2", "wrong conversation")));
        await dispatcher.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            dispatcher.DispatchAsync(new MessageOutput("conversation-1", "too late")));
        Assert.Throws<ObjectDisposedException>(() => dispatcher.Subscribe());
    }

    [Fact]
    public async Task Subscription_CanBeEnumeratedOnlyOnce()
    {
        await using var dispatcher = CreateDispatcher();
        var subscription = dispatcher.Subscribe();
        await dispatcher.DisposeAsync();

        Assert.Empty(await ReadAllAsync(subscription));
        Assert.Throws<InvalidOperationException>(() => subscription.ReadAllAsync());
    }

    private static ConversationOutputDispatcher CreateDispatcher() =>
        new(new ConversationSession(new ConversationContext("conversation-1", "subject-1")));

    private static async Task<List<ConversationOutput>> ReadAllAsync(
        IConversationOutputSubscription subscription, CancellationToken cancellationToken = default)
    {
        var outputs = new List<ConversationOutput>();
        await foreach (var output in subscription.ReadAllAsync(cancellationToken)) outputs.Add(output);
        return outputs;
    }
}
