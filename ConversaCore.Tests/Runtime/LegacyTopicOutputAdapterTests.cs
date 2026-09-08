using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Flow = ConversaCore.TopicFlow.TopicFlow;

namespace ConversaCore.Tests.Runtime;

/// <summary>Real event-path coverage for the CC-302 compatibility adapter.</summary>
public sealed class LegacyTopicOutputAdapterTests
{
    [Fact]
    public async Task FlowEvents_AreTranslatedInOrderWithoutExposingWorkflowContext()
    {
        var session = Session();
        await using var dispatcher = new ConversationOutputDispatcher(session);
        var subscription = dispatcher.Subscribe();
        var adapter = new LegacyTopicOutputAdapter(session, dispatcher,
            NullLogger<LegacyTopicOutputAdapter>.Instance);
        var activity = new EmittingCardActivity("collect");
        var flow = FlowWith(activity);
        await using var lease = adapter.Attach(Descriptor(), flow);

        await flow.RunAsync();
        await lease.DisposeAsync();
        activity.EmitMessage("after disposal");
        await dispatcher.DisposeAsync();
        var outputs = await ReadAllAsync(subscription);

        Assert.Contains(outputs, output => output is TopicLifecycleOutput
            { TopicId: "appointment", State: ConversationTopicState.Starting });
        Assert.Contains(outputs, output => output is ActivityLifecycleOutput
            { TopicId: "appointment", ActivityId: "collect", State: ConversationActivityState.Running });
        Assert.Contains(outputs, output => output is MessageOutput { Message: "hello" });
        Assert.DoesNotContain(outputs, output => output is MessageOutput { Message: "after disposal" });

        var cardSequence = outputs.Where(output => output is CardStateOutput or AdaptiveCardOutput or PromptStateOutput)
            .ToArray();
        Assert.Collection(cardSequence,
            output => Assert.Equal(("card-1", ConversationCardState.Active), CardState(output)),
            output => Assert.Equal("card-1", Assert.IsType<AdaptiveCardOutput>(output).CardId),
            output => Assert.Equal(ConversationPromptState.Disabled, Assert.IsType<PromptStateOutput>(output).State),
            output => Assert.Equal(("card-1", ConversationCardState.ReadOnly), CardState(output)),
            output => Assert.Equal(("card-2", ConversationCardState.Active), CardState(output)),
            output => Assert.Equal("card-2", Assert.IsType<AdaptiveCardOutput>(output).CardId),
            output => Assert.Equal(ConversationPromptState.Enabled, Assert.IsType<PromptStateOutput>(output).State));
    }

    [Fact]
    public async Task DispatchFailure_IsObservedAndDoesNotFaultTheLease()
    {
        var session = Session();
        await using var dispatcher = new FailingDispatcher();
        var adapter = new LegacyTopicOutputAdapter(session, dispatcher,
            NullLogger<LegacyTopicOutputAdapter>.Instance);
        var activity = new EmittingCardActivity("collect");
        var flow = FlowWith(activity);
        var lease = adapter.Attach(Descriptor(), flow);

        await flow.RunAsync();
        await lease.DisposeAsync();

        Assert.True(dispatcher.Attempts > 0);
    }

    [Fact]
    public async Task NonFlowTopic_AttachesAsNoOpCompatibilityLease()
    {
        var session = Session();
        await using var dispatcher = new ConversationOutputDispatcher(session);
        var adapter = new LegacyTopicOutputAdapter(session, dispatcher,
            NullLogger<LegacyTopicOutputAdapter>.Instance);

        var lease = adapter.Attach(Descriptor(), new PlainTopic());

        await lease.DisposeAsync();
    }

    [Fact]
    public async Task FireAndForgetEvent_IsMappedToImmutableNotificationWithoutBlockingForHost()
    {
        var session = Session();
        await using var dispatcher = new ConversationOutputDispatcher(session);
        await using var coordinator = new HostInteractionCoordinator(session, dispatcher);
        var subscription = dispatcher.Subscribe();
        var logger = new RecordingLogger<LegacyTopicOutputAdapter>();
        var adapter = new LegacyTopicOutputAdapter(session, dispatcher, logger, coordinator);
        var source = new MutablePayload { Value = "original" };
        var activity = new EventTriggerActivity("notify", "appointment.changed", source);
        var legacyCallbacks = 0;
        activity.CustomEventTriggered += (_, _) => legacyCallbacks++;
        await using var lease = adapter.Attach(Descriptor(), FlowWith(activity));
        await using var outputs = subscription.ReadAllAsync().GetAsyncEnumerator();

        var result = await activity.RunAsync(new TopicWorkflowContext());
        source.Value = "changed";
        var notification = await ReadUntilAsync<HostNotification<LegacyEventTriggerPayload>>(outputs);

        Assert.False(result.IsWaiting);
        Assert.Equal(ActivityState.Completed, activity.CurrentState);
        Assert.Equal("appointment.changed", notification.EventName);
        Assert.Equal(1, notification.Version);
        Assert.Equal("notify", notification.Payload.ActivityId);
        Assert.Equal("original", notification.Payload.Data.GetProperty("Value").GetString());
        Assert.Equal(0, legacyCallbacks);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning &&
            entry.Message.Contains("replace it with a typed host contract", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WaitForResponseEvent_UsesCorrelationAndResumesExactActivity()
    {
        var session = Session();
        await using var dispatcher = new ConversationOutputDispatcher(session);
        await using var coordinator = new HostInteractionCoordinator(session, dispatcher);
        var subscription = dispatcher.Subscribe();
        var adapter = new LegacyTopicOutputAdapter(session, dispatcher,
            NullLogger<LegacyTopicOutputAdapter>.Instance, coordinator);
        var activity = EventTriggerActivity.CreateWaitForResponse(
            "confirm", "appointment.confirm", "host_response",
            new { AppointmentId = "A-1" }, TimeSpan.FromSeconds(5));
        var context = new TopicWorkflowContext();
        await using var lease = adapter.Attach(Descriptor(), FlowWith(activity));
        await using var outputs = subscription.ReadAllAsync().GetAsyncEnumerator();

        var running = activity.RunAsync(context);
        var request = await ReadUntilAsync<HostInteractionRequest<LegacyEventTriggerPayload, JsonElement>>(outputs);

        Assert.False(running.IsCompleted);
        Assert.Equal(ActivityState.WaitingForUserInput, activity.CurrentState);
        Assert.True(session.IsHostInteractionPending(request.RequestId));
        Assert.Equal("A-1", request.Request.Data.GetProperty("AppointmentId").GetString());

        await coordinator.RespondAsync(new HostInteractionResponse(request.RequestId, new { Confirmed = true }));
        var result = await running;

        Assert.False(result.IsWaiting);
        Assert.Equal(ActivityState.Completed, activity.CurrentState);
        Assert.True(context.GetValue<JsonElement>("host_response").GetProperty("Confirmed").GetBoolean());
        Assert.Null(context.GetValue<string>("confirm_WaitingForEvent"));
        Assert.Empty(session.PendingHostInteractionIds);
    }

    [Fact]
    public async Task WaitForResponseCancellation_CleansPendingStateAndWaitingMarkers()
    {
        var session = Session();
        await using var dispatcher = new ConversationOutputDispatcher(session);
        await using var coordinator = new HostInteractionCoordinator(session, dispatcher);
        var subscription = dispatcher.Subscribe();
        var adapter = new LegacyTopicOutputAdapter(session, dispatcher,
            NullLogger<LegacyTopicOutputAdapter>.Instance, coordinator);
        var activity = EventTriggerActivity.CreateWaitForResponse(
            "confirm", "appointment.confirm", "host_response", responseTimeout: TimeSpan.FromSeconds(5));
        var context = new TopicWorkflowContext();
        using var cancellation = new CancellationTokenSource();
        await using var lease = adapter.Attach(Descriptor(), FlowWith(activity));
        await using var outputs = subscription.ReadAllAsync().GetAsyncEnumerator();

        var running = activity.RunAsync(context, cancellationToken: cancellation.Token);
        await ReadUntilAsync<HostInteractionRequest<LegacyEventTriggerPayload, JsonElement>>(outputs);
        cancellation.Cancel();
        var result = await running;

        Assert.True(result.IsCancelled);
        Assert.Equal(ActivityState.Failed, activity.CurrentState);
        Assert.Null(context.GetValue<string>("confirm_WaitingForEvent"));
        Assert.Empty(session.PendingHostInteractionIds);
    }

    [Fact]
    public async Task WaitForResponseTimeout_CancelsActivityAndRejectsLateResponse()
    {
        var session = Session();
        await using var dispatcher = new ConversationOutputDispatcher(session);
        await using var coordinator = new HostInteractionCoordinator(session, dispatcher);
        var subscription = dispatcher.Subscribe();
        var adapter = new LegacyTopicOutputAdapter(session, dispatcher,
            NullLogger<LegacyTopicOutputAdapter>.Instance, coordinator);
        var activity = EventTriggerActivity.CreateWaitForResponse(
            "confirm", "appointment.confirm", "host_response", responseTimeout: TimeSpan.FromMilliseconds(100));
        var context = new TopicWorkflowContext();
        await using var lease = adapter.Attach(Descriptor(), FlowWith(activity));
        await using var outputs = subscription.ReadAllAsync().GetAsyncEnumerator();

        var running = activity.RunAsync(context);
        var request = await ReadUntilAsync<HostInteractionRequest<LegacyEventTriggerPayload, JsonElement>>(outputs);
        var result = await running;

        Assert.True(result.IsCancelled);
        Assert.Equal(ActivityState.Failed, activity.CurrentState);
        Assert.Null(context.GetValue<string>("confirm_WaitingForEvent"));
        Assert.Empty(session.PendingHostInteractionIds);
        await Assert.ThrowsAsync<HostInteractionNotPendingException>(() => coordinator.RespondAsync(
            new HostInteractionResponse(request.RequestId, new { Confirmed = true })));
    }

    [Fact]
    public async Task DisposedLease_DetachesRuntimeCompatibilityHandler()
    {
        var session = Session();
        await using var dispatcher = new ConversationOutputDispatcher(session);
        var subscription = dispatcher.Subscribe();
        var adapter = new LegacyTopicOutputAdapter(session, dispatcher,
            NullLogger<LegacyTopicOutputAdapter>.Instance);
        var activity = new EventTriggerActivity("notify", "appointment.changed");
        var legacyCallbacks = 0;
        activity.CustomEventTriggered += (_, _) => legacyCallbacks++;
        var lease = adapter.Attach(Descriptor(), FlowWith(activity));

        await lease.DisposeAsync();
        await activity.RunAsync(new TopicWorkflowContext());
        await dispatcher.DisposeAsync();
        var outputs = await ReadAllAsync(subscription);

        Assert.Equal(1, legacyCallbacks);
        Assert.DoesNotContain(outputs, output => output is HostNotificationOutput);
    }

    private static (string CardId, ConversationCardState State) CardState(ConversationOutput output)
    {
        var state = Assert.IsType<CardStateOutput>(output);
        return (state.CardId, state.State);
    }

    private static ConversationSession Session() =>
        new(new ConversationContext("conversation-1", "subject-1"));

    private static TopicDescriptor Descriptor() =>
        new("appointment", _ => throw new NotSupportedException());

    private static Flow FlowWith(TopicFlowActivity activity)
    {
        var flow = new TestFlow();
        flow.Add(activity);
        return flow;
    }

    private static async Task<List<ConversationOutput>> ReadAllAsync(IConversationOutputSubscription subscription)
    {
        var outputs = new List<ConversationOutput>();
        await foreach (var output in subscription.ReadAllAsync()) outputs.Add(output);
        return outputs;
    }

    private static async Task<TOutput> ReadUntilAsync<TOutput>(IAsyncEnumerator<ConversationOutput> outputs)
        where TOutput : ConversationOutput
    {
        while (await outputs.MoveNextAsync())
            if (outputs.Current is TOutput match) return match;
        throw new InvalidOperationException($"Output {typeof(TOutput).Name} was not dispatched.");
    }

    private sealed class MutablePayload
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class EmittingCardActivity(string id) : TopicFlowActivity(id), IAdaptiveCardActivity
    {
        public event EventHandler<CardJsonEventArgs>? CardJsonEmitted { add { } remove { } }
        public event EventHandler<CardJsonEventArgs>? CardJsonSending { add { } remove { } }
        public event EventHandler<CardJsonEventArgs>? CardJsonSent;
        public event EventHandler<CardJsonRenderedEventArgs>? CardJsonRendered { add { } remove { } }
        public event EventHandler<CardDataReceivedEventArgs>? CardDataReceived { add { } remove { } }
        public event EventHandler<ModelBoundEventArgs>? ModelBound { add { } remove { } }
        public event EventHandler<ValidationFailedEventArgs>? ValidationFailed { add { } remove { } }

        protected override Task<ActivityResult> RunActivity(TopicWorkflowContext context, object? input = null,
            CancellationToken cancellationToken = default)
        {
            OnMessageEmitted("hello");
            CardJsonSent?.Invoke(this,
                new CardJsonEventArgs("{}", "first", RenderMode.Append, "card-1", isRequired: true));
            CardJsonSent?.Invoke(this,
                new CardJsonEventArgs("{}", "second", RenderMode.Replace, "card-2", isRequired: false));
            return Task.FromResult(ActivityResult.Continue("done"));
        }

        public void EmitMessage(string message) => OnMessageEmitted(message);
        public void OnInputCollected(AdaptiveCardInputCollectedEventArgs e) { }
    }

    private sealed class PlainTopic : ITopic
    {
        public string Name => "plain";
        public int Priority => 0;
        public Task<ConversaCore.Models.TopicResult> ProcessMessageAsync(string message,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(0f);
    }

    private sealed class TestFlow()
        : Flow(new TopicWorkflowContext(), NullLogger.Instance, "Appointment")
    {
        public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(1f);
    }

    private sealed class FailingDispatcher : IConversationOutputDispatcher
    {
        public int Attempts { get; private set; }
        public Task DispatchAsync(ConversationOutput output, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Task.FromException(new ApplicationException("dispatch failed"));
        }
        public IConversationOutputSubscription Subscribe() => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
