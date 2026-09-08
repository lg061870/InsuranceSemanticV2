using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Microsoft.Extensions.Logging.Abstractions;
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
