using ConversaCore.Agentic;
using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Microsoft.Extensions.Logging.Abstractions;
using Flow = ConversaCore.TopicFlow.TopicFlow;

namespace ConversaCore.Tests.Characterization;

/// <summary>
/// Exercises the fallback interruption/resumption path inside
/// <see cref="DomainAgentService.ProcessUserMessageAsync"/> (the "pause the active topic" branch)
/// and <see cref="DomainAgentService.HandleFallbackCompletion"/> (the resumption branch) through
/// the real callbacks, using a real <see cref="TopicRegistry"/> and a real
/// <see cref="AdaptiveCardActivity{TModel}"/>-derived pausable activity. These are legacy
/// characterization tests: passing means today's behavior (defects included) is reproduced,
/// not that the behavior is correct.
/// </summary>
public class LegacyFallbackInterruptionTests {
    private const string StartMessage = "start please";

    [Fact]
    public async Task UnmatchedMessage_PausesActiveTopicAndActivatesFallback() {
        var registry = new TopicRegistry();
        var active = new InterruptibleTopic();
        var card = new ResumeTrackingCard(active.Context);
        active.Add(card);
        var fallback = new FallbackProbe();
        registry.RegisterTopic(active);
        registry.RegisterTopic(fallback);

        var agent = new AgentProbe(registry);
        var cardEvents = new List<CardStateChangedEventArgs>();
        agent.CardStateChanged += (_, e) => cardEvents.Add(e);

        // Drive the topic into a real WaitingForUserInput state via the actual matched-topic path.
        await agent.Message(StartMessage);

        Assert.Same(active, agent.Active);
        Assert.Equal(Flow.FlowState.WaitingForInput, active.State);
        Assert.Equal(ActivityState.WaitingForUserInput, card.CurrentState);
        Assert.True(card.IsPaused);
        Assert.Equal(card.Id, agent.ActiveCardId);

        // Now interrupt with a message no topic can handle.
        await agent.Message("gibberish nonsense");

        Assert.Same(fallback, agent.Active);
        Assert.Equal(1, fallback.RunCount);
        Assert.Equal("gibberish nonsense", fallback.Context.GetValue<string>("Fallback_UserPrompt"));
        Assert.Equal(1, agent.PausedCount);
        Assert.Same(active, agent.PeekPaused());

        // UnhookTopicEvents deactivated the lingering card on the way out...
        Assert.Contains(cardEvents, e => e.CardId == card.Id && e.State == CardState.ReadOnly);

        // ...but this is a defect: UnhookTopicEvents reads the active-card marker from
        // IConversationContext (_context) yet clears it from the unrelated TopicWorkflowContext
        // (_wfContext), so the marker on _context survives the "unhook" untouched.
        Assert.Equal(card.Id, agent.ActiveCardId);
    }

    [Fact]
    public async Task FallbackCompletion_ResumesPausedTopic_ButDoesNotAdvanceItsFlowState() {
        var registry = new TopicRegistry();
        var active = new InterruptibleTopic();
        var card = new ResumeTrackingCard(active.Context);
        active.Add(card);
        var fallback = new FallbackProbe();
        registry.RegisterTopic(active);
        registry.RegisterTopic(fallback);

        var agent = new AgentProbe(registry);
        var cardEvents = new List<CardStateChangedEventArgs>();
        agent.CardStateChanged += (_, e) => cardEvents.Add(e);

        await agent.Message(StartMessage);
        await agent.Message("gibberish nonsense");

        var readOnlyBeforeCompletion = cardEvents.Count(e => e.CardId == card.Id && e.State == CardState.ReadOnly);

        var result = TopicResult.CreateCompleted("Fallback completed", fallback.Context);
        await agent.CompleteFallback(fallback, result);

        Assert.Same(active, agent.Active);
        Assert.Equal(0, agent.PausedCount);

        // HandleFallbackCompletion unhooks FallbackTopic too, and because the active-card marker
        // was never actually cleared (see the previous test), that unhook fires a SECOND, redundant
        // ReadOnly notification for the same card before it gets "restored".
        Assert.Equal(readOnlyBeforeCompletion + 1, cardEvents.Count(e => e.CardId == card.Id && e.State == CardState.ReadOnly));
        Assert.Equal(CardState.Active, cardEvents.Last().State);
        Assert.Equal(card.Id, cardEvents.Last().CardId);

        // The paused activity's own ResumeAsync override IS invoked with the literal
        // "FallbackTopic completed" input and CancellationToken.None, exactly as the source
        // hard-codes it in HandleFallbackCompletion.
        Assert.Equal(1, card.ResumeCallCount);
        Assert.Equal("FallbackTopic completed", card.LastResumeInput);
        Assert.Equal(CancellationToken.None, card.LastResumeToken);

        // Defect #1: TopicFlowActivity.ResumeAsync's base implementation only raises
        // ActivityLifecycleChanged; it never calls TransitionTo, so the activity's own
        // CurrentState does not actually change back from WaitingForUserInput.
        Assert.Equal(ActivityState.WaitingForUserInput, card.CurrentState);

        // Defect #2: every TopicFlowActivity implements IPausableActivity, so
        // "GetCurrentActivity() as IPausableActivity" in HandleFallbackCompletion is never null
        // and the "no pausable activity found" branch that calls the FLOW-level
        // TopicFlow.ResumeAsync (which would call StepAsync and truly advance the FSM) is
        // effectively dead code. The flow itself is left stuck in WaitingForInput even though
        // the topic is active again.
        Assert.Equal(Flow.FlowState.WaitingForInput, active.State);
    }

    // ================================================================
    // Probes
    // ================================================================

    private sealed class AgentProbe(TopicRegistry registry) : DomainAgentService(
        registry, new ConversationContext("test", "user"), new TopicWorkflowContext(),
        NullLogger<DomainAgentService>.Instance) {
        public ITopic? Active => _activeTopic;
        public int PausedCount => _pausedTopics.Count;
        public Flow? PeekPaused() => _pausedTopics.Count > 0 ? _pausedTopics.Peek() : null;
        public string? ActiveCardId => _context.GetValue<string>(ActiveCardKey);
        public Task Message(string input) => ProcessUserMessageAsync(input);
        public Task CompleteFallback(Flow fallback, TopicResult result) => HandleFallbackCompletion(fallback, result);
    }

    // Matches the "active" topic on a known trigger phrase only, so the second (unrecognized)
    // message leaves every registered topic below the 0.3 confidence threshold and forces the
    // real fallback branch in ProcessUserMessageAsync.
    private sealed class InterruptibleTopic() : Flow(new TopicWorkflowContext(), NullLogger.Instance, "ActiveTopic") {
        public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => Task.FromResult(message == StartMessage ? 0.9f : 0f);
    }

    // Deterministic stand-in for the registered FallbackTopic, same pattern as
    // LegacyOrchestrationTests.RoutingProbe: never matches directly, and RunAsync is overridden
    // so we can observe it ran without depending on an activity queue.
    private sealed class FallbackProbe() : Flow(new TopicWorkflowContext(), NullLogger.Instance, "FallbackTopic") {
        public int RunCount { get; private set; }
        public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => Task.FromResult(0f);
        public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
            RunCount++;
            return Task.FromResult(TopicResult.CreateResponse("fallback ran", Context));
        }
    }

    // A real AdaptiveCardActivity<TModel> (the concrete IPausableActivity used throughout the
    // production flows). It renders to WaitingForUserInput like any required card, and overrides
    // ResumeAsync purely to record how the real callback invokes it.
    private sealed class ResumeTrackingCard(TopicWorkflowContext context)
        : AdaptiveCardActivity<ResumeTrackingCard.ProbeModel>(
            "probe-card", context, NullLogger<AdaptiveCardActivity<ProbeModel>>.Instance, "probe_model") {
        public int ResumeCallCount { get; private set; }
        public string? LastResumeInput { get; private set; }
        public CancellationToken LastResumeToken { get; private set; }

        protected override string GetCardJson(TopicWorkflowContext context) => """
            {"type":"AdaptiveCard","version":"1.5","body":[]}
            """;

        public override Task ResumeAsync(string? input = null, CancellationToken cancellationToken = default) {
            ResumeCallCount++;
            LastResumeInput = input;
            LastResumeToken = cancellationToken;
            return base.ResumeAsync(input, cancellationToken);
        }

        public sealed class ProbeModel {
            public string? Name { get; set; }
        }
    }
}
