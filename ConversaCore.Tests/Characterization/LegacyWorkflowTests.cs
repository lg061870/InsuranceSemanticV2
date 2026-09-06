using ConversaCore.Context;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging.Abstractions;
using Flow = ConversaCore.TopicFlow.TopicFlow;

namespace ConversaCore.Tests.Characterization;

public class LegacyWorkflowTests {
    [Fact]
    public async Task FlowReset_RemovesAuthoredActivities_AndRequiresRebuildingBeforeRerun() {
        var flow = new ProbeFlow();
        flow.Add(new SimpleActivity("hello", "hello"));
        Assert.True((await flow.RunAsync()).IsCompleted);

        flow.Reset();

        Assert.Equal(Flow.FlowState.Idle, flow.State);
        Assert.Empty(flow.GetAllActivities());
        await Assert.ThrowsAsync<InvalidOperationException>(() => flow.RunAsync());
    }

    [Fact]
    public async Task SubtopicWait_PreservesParentCursor_ButTriggerActivityIsAlreadyCompleted() {
        var context = new ConversationContext("session", "user");
        context.SetCurrentTopic("parent");
        var flow = new ProbeFlow();
        var trigger = new TriggerTopicActivity("call", "child", waitForCompletion: true, conversationContext: context);
        var after = new SimpleActivity("after", "returned");
        flow.Add(trigger).Add(after);

        var result = await flow.RunAsync();

        Assert.True(result.IsWaitingForSubTopic);
        Assert.Equal("child", result.NextTopicName);
        Assert.Equal(Flow.FlowState.WaitingForInput, flow.State);
        Assert.Same(trigger, flow.GetCurrentActivity());
        Assert.Equal(ActivityState.Completed, trigger.CurrentState);
        Assert.Equal(ActivityState.Created, after.CurrentState);
        Assert.Equal(1, context.GetTopicCallDepth());

        var returned = context.PopTopicCall("child result");
        Assert.Equal("parent", returned!.CallingTopicName);
        Assert.Equal("child result", returned.CompletionData);
        Assert.True((await flow.ResumeAsync("child result")).IsCompleted);
        Assert.Equal(ActivityState.Completed, after.CurrentState);
    }

    [Fact]
    public void RequiredCardFlag_IsHiddenWhenReadThroughActivityBaseType() {
        var card = new ProbeCard(new TopicWorkflowContext()) { IsRequired = true };

        Assert.True(card.IsRequired);
        Assert.False(((TopicFlowActivity)card).IsRequired);
    }

    [Fact]
    public void ConversationReset_ClearsStateHistoryChainAndCallStack_ButKeepsIdentity() {
        var context = new ConversationContext("session", "user");
        context.SetValue("answer", "value");
        context.SetCurrentTopic("parent");
        context.AddTopicToChain("queued");
        context.PushTopicCall("parent", "child");

        context.Reset();

        Assert.False(context.HasValue("answer"));
        Assert.Null(context.CurrentTopicName);
        Assert.Empty(context.TopicHistory);
        Assert.Empty(context.TopicChain);
        Assert.Equal(0, context.GetTopicCallDepth());
        Assert.Equal("session", context.ConversationId);
        Assert.Equal("user", context.UserId);
    }

    private sealed class ProbeFlow() : Flow(new TopicWorkflowContext(), NullLogger.Instance, "parent");
    private sealed class ProbeCard(TopicWorkflowContext context)
        : AdaptiveCardActivity<object>("card", context, NullLogger<AdaptiveCardActivity<object>>.Instance) {
        protected override string GetCardJson(TopicWorkflowContext context) => "{\"type\":\"AdaptiveCard\",\"version\":\"1.5\",\"body\":[]}";
    }
}
