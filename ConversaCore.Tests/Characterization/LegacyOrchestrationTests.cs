using ConversaCore.Agentic;
using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Microsoft.Extensions.Logging.Abstractions;
using Flow = ConversaCore.TopicFlow.TopicFlow;

namespace ConversaCore.Tests.Characterization;

public class LegacyOrchestrationTests {
    [Fact]
    public async Task FrameworkStart_RequiresDomainOverride_EvenWithRegisteredStartTopic() {
        var registry = new TopicRegistry();
        var start = new RoutingProbe("ConversationStart", 1);
        registry.RegisterTopic(start);
        var agent = new AgentProbe(registry);

        await Assert.ThrowsAsync<NotImplementedException>(() => agent.Start());
        Assert.Equal(0, start.RunCount);
    }

    [Fact]
    public async Task Message_ReevaluatesAllTopics_WithoutGivingActiveTopicFirstRefusal() {
        var registry = new TopicRegistry();
        var active = new RoutingProbe("active", 0.4f);
        var competing = new RoutingProbe("competing", 0.9f);
        registry.RegisterTopic(active);
        registry.RegisterTopic(competing);
        var agent = new AgentProbe(registry);
        agent.Activate(active);

        await agent.Message("answer for active topic");

        Assert.Same(competing, agent.Active);
        Assert.Equal(1, active.MatchCount);
        Assert.Equal(1, competing.MatchCount);
        Assert.Equal(0, active.RunCount);
        Assert.Equal(1, competing.RunCount);
    }

    [Fact]
    public async Task MatchedFlow_IsRunWithoutPassingUserMessageToProcessMessage() {
        var registry = new TopicRegistry();
        var topic = new RoutingProbe("matched", 1);
        registry.RegisterTopic(topic);
        var agent = new AgentProbe(registry);

        await agent.Message("customer answer");

        Assert.Equal(1, topic.RunCount);
        Assert.Equal(0, topic.ProcessCount);
        Assert.Equal("customer answer", topic.LastMatchInput);
    }

    [Fact]
    public async Task UnmatchedMessage_IsStoredOnFallbackContext() {
        var registry = new TopicRegistry();
        var fallback = new RoutingProbe("FallbackTopic", 0);
        registry.RegisterTopic(fallback);
        var agent = new AgentProbe(registry);

        await agent.Message("unrecognized request");

        Assert.Same(fallback, agent.Active);
        Assert.Equal("unrecognized request", fallback.Context.GetValue<string>("Fallback_UserPrompt"));
        Assert.Equal(1, fallback.RunCount);
    }

    [Fact]
    public async Task NoMatchWithoutFallback_EmitsOneMissingTopicNotification() {
        var agent = new AgentProbe(new TopicRegistry());
        var notifications = 0;
        agent.MatchingTopicNotFound += (_, _) => notifications++;

        await agent.Message("unknown");

        Assert.Equal(1, notifications);
        Assert.Null(agent.Active);
    }

    [Fact]
    public void AsyncCompletion_InsertsFollowupIntoActiveFlow_AndForwardsOriginalEvent() {
        var agent = new AgentProbe(new TopicRegistry());
        var flow = new RoutingProbe("active", 1);
        flow.Add(new SimpleActivity("existing", "existing"));
        agent.Activate(flow);
        var followup = new SimpleActivity("followup", "result");
        var args = new AsyncQueryCompletedEventArgs(flow.Context, "result", "query", followup);
        AsyncQueryCompletedEventArgs? forwarded = null;
        agent.AsyncActivityCompleted += (_, e) => forwarded = e;

        agent.CompleteAsyncQuery(args);

        Assert.Contains(followup, flow.GetAllActivities());
        Assert.Equal(ActivityState.Created, followup.CurrentState);
        Assert.Same(args, forwarded);
    }

    private sealed class AgentProbe(TopicRegistry registry) : DomainAgentService(
        registry, new ConversationContext("test", "user"), new TopicWorkflowContext(),
        NullLogger<DomainAgentService>.Instance) {
        public ITopic? Active => _activeTopic;
        public void Activate(ITopic topic) => _activeTopic = topic;
        public Task Start() => OnConversationStartRequestedAsync(default);
        public Task Message(string input) => ProcessUserMessageAsync(input);
        public void CompleteAsyncQuery(AsyncQueryCompletedEventArgs args) => HandleAsyncActivityCompleted(this, args);
    }

    // Keep matching deterministic and observe which TopicFlow entry point the actual agent chooses.
    private sealed class RoutingProbe(string name, float confidence)
        : Flow(new TopicWorkflowContext(), NullLogger.Instance, name) {
        public int MatchCount { get; private set; }
        public int RunCount { get; private set; }
        public int ProcessCount { get; private set; }
        public string? LastMatchInput { get; private set; }
        public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) {
            MatchCount++;
            LastMatchInput = message;
            return Task.FromResult(confidence);
        }
        public override Task<TopicResult> RunAsync(CancellationToken cancellationToken = default) {
            RunCount++;
            return Task.FromResult(TopicResult.CreateResponse("probe", Context));
        }
        public override Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) {
            ProcessCount++;
            return Task.FromResult(TopicResult.CreateResponse("probe", Context));
        }
    }
}
