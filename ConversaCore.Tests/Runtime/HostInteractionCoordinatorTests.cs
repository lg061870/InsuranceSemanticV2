using ConversaCore.Context;
using ConversaCore.Runtime;

namespace ConversaCore.Tests.Runtime;

/// <summary>Correlation, timeout, cancellation, and failure coverage for CC-304.</summary>
public sealed class HostInteractionCoordinatorTests
{
    [Fact]
    public async Task RequestAsync_DispatchesTypedFrozenRequestAndReturnsMatchingTypedResponse()
    {
        var (session, dispatcher, coordinator) = Create();
        await using var dispatcherLease = dispatcher;
        await using var coordinatorLease = coordinator;
        var subscription = dispatcher.Subscribe();
        var source = new RequestPayload { AppointmentId = "A-1" };
        var pending = coordinator.RequestAsync<RequestPayload, ResponsePayload>(
            "appointment.confirm", 1, source, TimeSpan.FromSeconds(5));
        source.AppointmentId = "changed";

        var request = Assert.IsType<HostInteractionRequest<RequestPayload, ResponsePayload>>(
            await ReadOneAsync(subscription));
        Assert.Equal("A-1", request.Request.AppointmentId);
        Assert.Equal(typeof(ResponsePayload), request.ResponseType);
        Assert.True(session.IsHostInteractionPending(request.RequestId));

        await coordinator.RespondAsync(new HostInteractionResponse<ResponsePayload>(
            request.RequestId, new ResponsePayload { Confirmed = true }));

        Assert.True((await pending).Confirmed);
        Assert.Empty(session.PendingHostInteractionIds);
    }

    [Fact]
    public async Task WrongResponseType_IsRejectedWithoutConsumingPendingRequest()
    {
        var (session, dispatcher, coordinator) = Create();
        await using var dispatcherLease = dispatcher;
        await using var coordinatorLease = coordinator;
        var subscription = dispatcher.Subscribe();
        var pending = coordinator.RequestAsync<RequestPayload, ResponsePayload>(
            "appointment.confirm", 1, new RequestPayload(), TimeSpan.FromSeconds(5));
        var request = Assert.IsAssignableFrom<HostInteractionRequestOutput>(await ReadOneAsync(subscription));

        var exception = await Assert.ThrowsAsync<HostInteractionResponseTypeException>(() =>
            coordinator.RespondAsync(new HostInteractionResponse<string>(request.RequestId, "wrong")));

        Assert.Equal(typeof(ResponsePayload), exception.ExpectedType);
        Assert.True(session.IsHostInteractionPending(request.RequestId));
        await coordinator.RespondAsync(new HostInteractionResponse<ResponsePayload>(
            request.RequestId, new ResponsePayload { Confirmed = true }));
        Assert.True((await pending).Confirmed);
    }

    [Fact]
    public async Task DuplicateResponse_IsRejectedAfterExactlyOneCompletion()
    {
        var (_, dispatcher, coordinator) = Create();
        await using var dispatcherLease = dispatcher;
        await using var coordinatorLease = coordinator;
        var subscription = dispatcher.Subscribe();
        var pending = coordinator.RequestAsync<RequestPayload, ResponsePayload>(
            "appointment.confirm", 1, new RequestPayload(), TimeSpan.FromSeconds(5));
        var request = Assert.IsAssignableFrom<HostInteractionRequestOutput>(await ReadOneAsync(subscription));
        var response = new HostInteractionResponse<ResponsePayload>(
            request.RequestId, new ResponsePayload { Confirmed = true });

        await coordinator.RespondAsync(response);
        await pending;

        await Assert.ThrowsAsync<HostInteractionNotPendingException>(() => coordinator.RespondAsync(response));
    }

    [Fact]
    public async Task Timeout_RemovesPendingStateAndRejectsLateResponse()
    {
        var (session, dispatcher, coordinator) = Create();
        await using var dispatcherLease = dispatcher;
        await using var coordinatorLease = coordinator;
        var subscription = dispatcher.Subscribe();
        var pending = coordinator.RequestAsync<RequestPayload, ResponsePayload>(
            "appointment.confirm", 1, new RequestPayload(), TimeSpan.FromMilliseconds(50));
        var request = Assert.IsAssignableFrom<HostInteractionRequestOutput>(await ReadOneAsync(subscription));

        var timeout = await Assert.ThrowsAsync<HostInteractionTimeoutException>(() => pending);

        Assert.Equal(request.RequestId, timeout.RequestId);
        Assert.Empty(session.PendingHostInteractionIds);
        await Assert.ThrowsAsync<HostInteractionNotPendingException>(() => coordinator.RespondAsync(
            new HostInteractionResponse<ResponsePayload>(request.RequestId, new ResponsePayload())));
    }

    [Fact]
    public async Task CallerCancellation_RemovesPendingState()
    {
        var (session, dispatcher, coordinator) = Create();
        await using var dispatcherLease = dispatcher;
        await using var coordinatorLease = coordinator;
        var subscription = dispatcher.Subscribe();
        using var cancellation = new CancellationTokenSource();
        var pending = coordinator.RequestAsync<RequestPayload, ResponsePayload>(
            "appointment.confirm", 1, new RequestPayload(), TimeSpan.FromSeconds(5), cancellation.Token);
        await ReadOneAsync(subscription);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Empty(session.PendingHostInteractionIds);
    }

    [Fact]
    public async Task ConcurrentRequests_AreResolvedOnlyByTheirOwnCorrelationIds()
    {
        var (_, dispatcher, coordinator) = Create();
        await using var dispatcherLease = dispatcher;
        await using var coordinatorLease = coordinator;
        var subscription = dispatcher.Subscribe();
        var first = coordinator.RequestAsync<RequestPayload, ResponsePayload>(
            "appointment.confirm", 1, new RequestPayload { AppointmentId = "first" }, TimeSpan.FromSeconds(5));
        var second = coordinator.RequestAsync<RequestPayload, ResponsePayload>(
            "appointment.confirm", 1, new RequestPayload { AppointmentId = "second" }, TimeSpan.FromSeconds(5));
        var requests = await ReadAsync(subscription, 2);
        var firstRequest = Assert.IsType<HostInteractionRequest<RequestPayload, ResponsePayload>>(requests[0]);
        var secondRequest = Assert.IsType<HostInteractionRequest<RequestPayload, ResponsePayload>>(requests[1]);

        await coordinator.RespondAsync(new HostInteractionResponse<ResponsePayload>(
            secondRequest.RequestId, new ResponsePayload { Label = "second" }));
        await coordinator.RespondAsync(new HostInteractionResponse<ResponsePayload>(
            firstRequest.RequestId, new ResponsePayload { Label = "first" }));

        Assert.Equal("first", (await first).Label);
        Assert.Equal("second", (await second).Label);
    }

    [Fact]
    public async Task DispatchFailureAndCoordinatorDisposal_ClearAndCompletePendingRequests()
    {
        var session = Session();
        await using var failing = new FailingDispatcher();
        await using var failingCoordinator = new HostInteractionCoordinator(session, failing);
        await Assert.ThrowsAsync<ApplicationException>(() =>
            failingCoordinator.RequestAsync<RequestPayload, ResponsePayload>(
                "appointment.confirm", 1, new RequestPayload(), TimeSpan.FromSeconds(5)));
        Assert.Empty(session.PendingHostInteractionIds);

        await using var dispatcher = new ConversationOutputDispatcher(session);
        var coordinator = new HostInteractionCoordinator(session, dispatcher);
        var subscription = dispatcher.Subscribe();
        var pending = coordinator.RequestAsync<RequestPayload, ResponsePayload>(
            "appointment.confirm", 1, new RequestPayload(), TimeSpan.FromSeconds(5));
        await ReadOneAsync(subscription);

        await coordinator.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        Assert.Empty(session.PendingHostInteractionIds);
    }

    private static (ConversationSession Session, ConversationOutputDispatcher Dispatcher,
        HostInteractionCoordinator Coordinator) Create()
    {
        var session = Session();
        var dispatcher = new ConversationOutputDispatcher(session);
        return (session, dispatcher, new HostInteractionCoordinator(session, dispatcher));
    }

    private static ConversationSession Session() =>
        new(new ConversationContext("conversation-1", "subject-1"));

    private static async Task<ConversationOutput> ReadOneAsync(IConversationOutputSubscription subscription) =>
        Assert.Single(await ReadAsync(subscription, 1));

    private static async Task<List<ConversationOutput>> ReadAsync(
        IConversationOutputSubscription subscription, int count)
    {
        var outputs = new List<ConversationOutput>();
        await using var enumerator = subscription.ReadAllAsync().GetAsyncEnumerator();
        while (outputs.Count < count && await enumerator.MoveNextAsync()) outputs.Add(enumerator.Current);
        return outputs;
    }

    public sealed class RequestPayload
    {
        public string AppointmentId { get; set; } = string.Empty;
    }

    public sealed class ResponsePayload
    {
        public bool Confirmed { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    private sealed class FailingDispatcher : IConversationOutputDispatcher
    {
        public Task DispatchAsync(ConversationOutput output, CancellationToken cancellationToken = default) =>
            Task.FromException(new ApplicationException("dispatch failed"));
        public IConversationOutputSubscription Subscribe() => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
