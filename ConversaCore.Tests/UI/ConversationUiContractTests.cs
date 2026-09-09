using System.Collections.Concurrent;
using ConversaCore.Context;
using ConversaCore.Runtime;
using ConversaCore.UI.Lifecycle;
using ConversaCore.UI.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConversaCore.Tests.UI;

/// <summary>End-to-end contracts across the runtime-to-ConversaCore.UI boundary.</summary>
public sealed class ConversationUiContractTests
{
    [Fact]
    public async Task StandardOutput_IsProjectedInOrder_AndResetClearsPresentationAndRuntime()
    {
        var environment = new ContractEnvironment();
        await using var environmentLease = environment;
        var state = new ConversationOutputViewState();
        var processed = SignalAfter(5);
        await using var binding = environment.CreateBinding((output, _) =>
        {
            state.Apply(output);
            processed.Signal();
            return Task.CompletedTask;
        });
        await binding.StartAsync();

        await environment.Dispatcher.DispatchAsync(new MessageOutput(environment.ConversationId, "Welcome"));
        await environment.Dispatcher.DispatchAsync(new AdaptiveCardOutput(
            environment.ConversationId, "appointment", "{\"version\":1}"));
        await environment.Dispatcher.DispatchAsync(new PromptStateOutput(
            environment.ConversationId, ConversationPromptState.Disabled, "appointment"));
        await environment.Dispatcher.DispatchAsync(new AdaptiveCardOutput(
            environment.ConversationId, "appointment", "{\"version\":2}",
            ConversationCardRenderMode.Replace));
        await environment.Dispatcher.DispatchAsync(new PromptStateOutput(
            environment.ConversationId, ConversationPromptState.Enabled, "appointment"));
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(state.IsPromptEnabled);
        Assert.Collection(state.Messages,
            message => Assert.Equal("Welcome", message.Content),
            card => Assert.Equal("{\"version\":2}", card.AdaptiveCardJson));

        state.Clear();
        await environment.Runtime.ResetAsync();

        Assert.Empty(state.Messages);
        Assert.Equal(1, environment.Runtime.ResetCount);
    }

    [Fact]
    public async Task HostNotification_PublicationDoesNotWaitForUiConsumer()
    {
        var environment = new ContractEnvironment();
        await using var environmentLease = environment;
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var binding = environment.CreateBinding(async (output, _) =>
        {
            Assert.IsAssignableFrom<HostNotificationOutput>(output);
            callbackEntered.TrySetResult();
            await releaseCallback.Task;
        });
        await binding.StartAsync();
        var notification = new HostNotification<string>(
            environment.ConversationId, "appointment.updated", 1, "A-100");

        var publication = environment.Dispatcher.DispatchAsync(notification);

        Assert.True(publication.IsCompletedSuccessfully);
        await callbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        releaseCallback.TrySetResult();
    }

    [Fact]
    public async Task HostInteraction_UiResponseResumesTheExactPendingRequest()
    {
        var environment = new ContractEnvironment();
        await using var environmentLease = environment;
        await using var binding = environment.CreateBinding(async (output, cancellationToken) =>
        {
            var hostOutput = Assert.IsAssignableFrom<HostOutput>(output);
            var context = new ConversationHostOutputContext(hostOutput, environment.Runtime);
            await context.RespondAsync(new Confirmation(true), cancellationToken);
        });
        await binding.StartAsync();

        var response = await environment.Coordinator.RequestAsync<string, Confirmation>(
            "appointment.confirm", 1, "A-100", TimeSpan.FromSeconds(2));

        Assert.True(response.Accepted);
        Assert.Empty(environment.Session.PendingHostInteractionIds);
    }

    [Fact]
    public async Task HostInteraction_WithoutUiResponseTimesOutAndLeavesNoPendingState()
    {
        var environment = new ContractEnvironment();
        await using var environmentLease = environment;
        var requestObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var binding = environment.CreateBinding((output, _) =>
        {
            Assert.IsAssignableFrom<HostInteractionRequestOutput>(output);
            requestObserved.TrySetResult();
            return Task.CompletedTask;
        });
        await binding.StartAsync();

        var pending = environment.Coordinator.RequestAsync<string, Confirmation>(
            "appointment.confirm", 1, "A-100", TimeSpan.FromMilliseconds(100));
        await requestObserved.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.ThrowsAsync<HostInteractionTimeoutException>(() => pending);
        Assert.Empty(environment.Session.PendingHostInteractionIds);
    }

    [Fact]
    public async Task DisconnectThenReconnect_ReleasesOldConsumerAndUsesFreshSubscription()
    {
        var environment = new ContractEnvironment();
        await using var environmentLease = environment;
        var firstOutputs = new ConcurrentQueue<string>();
        var firstReceived = SignalAfter(1);
        var first = environment.CreateBinding((output, _) =>
        {
            firstOutputs.Enqueue(Assert.IsType<MessageOutput>(output).Message);
            firstReceived.Signal();
            return Task.CompletedTask;
        });
        await first.StartAsync();
        await environment.Dispatcher.DispatchAsync(new MessageOutput(environment.ConversationId, "before disconnect"));
        await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await first.DisposeAsync();
        await environment.Dispatcher.DispatchAsync(new MessageOutput(environment.ConversationId, "while disconnected"));

        var secondOutputs = new ConcurrentQueue<string>();
        var secondReceived = SignalAfter(1);
        await using var second = environment.CreateBinding((output, _) =>
        {
            secondOutputs.Enqueue(Assert.IsType<MessageOutput>(output).Message);
            secondReceived.Signal();
            return Task.CompletedTask;
        });
        await second.StartAsync();
        await environment.Dispatcher.DispatchAsync(new MessageOutput(environment.ConversationId, "after reconnect"));
        await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(["before disconnect"], firstOutputs);
        Assert.Equal(["after reconnect"], secondOutputs);
        Assert.Equal(2, environment.Runtime.StartCount);
    }

    private static CompletionSignal SignalAfter(int count) => new(count);

    private sealed record Confirmation(bool Accepted);

    private sealed class CompletionSignal(int remaining)
    {
        private int _remaining = remaining;
        private readonly TaskCompletionSource _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Task => _completion.Task;

        public void Signal()
        {
            if (Interlocked.Decrement(ref _remaining) == 0)
                _completion.TrySetResult();
        }
    }

    private sealed class ContractEnvironment : IAsyncDisposable
    {
        public string ConversationId => Session.ConversationId;
        public ConversationSession Session { get; } =
            new(new ConversationContext("ui-contract", "subject-1"));
        public ConversationOutputDispatcher Dispatcher { get; }
        public HostInteractionCoordinator Coordinator { get; }
        public ContractRuntime Runtime { get; }

        public ContractEnvironment()
        {
            Dispatcher = new ConversationOutputDispatcher(Session);
            Coordinator = new HostInteractionCoordinator(Session, Dispatcher);
            Runtime = new ContractRuntime(ConversationId, Dispatcher, Coordinator);
        }

        public ConversationOutputSubscriptionOwner CreateBinding(
            Func<ConversationOutput, CancellationToken, Task> handler) =>
            new(Runtime, handler, NullLogger.Instance);

        public async ValueTask DisposeAsync()
        {
            await Coordinator.DisposeAsync();
            await Dispatcher.DisposeAsync();
        }
    }

    private sealed class ContractRuntime(
        string conversationId,
        IConversationOutputDispatcher dispatcher,
        IHostInteractionCoordinator coordinator) : IConversationRuntime
    {
        public string ConversationId { get; } = conversationId;
        public int StartCount { get; private set; }
        public int ResetCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task SendMessageAsync(string message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SubmitCardAsync(CardSubmission submission, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RespondToHostInteractionAsync(
            HostInteractionResponse response,
            CancellationToken cancellationToken = default) =>
            coordinator.RespondAsync(response, cancellationToken);

        public Task ResetAsync(CancellationToken cancellationToken = default)
        {
            ResetCount++;
            return Task.CompletedTask;
        }

        public IConversationOutputSubscription Subscribe() => dispatcher.Subscribe();
    }
}
