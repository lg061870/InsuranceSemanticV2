using ConversaCore.TopicFlow;

namespace ConversaCore.Tests.Characterization;

/// <summary>Executable evidence for defects to remove in the host interaction runtime.</summary>
public class LegacyHostInteractionTests {
    [Fact]
    public async Task Notification_ResolvesDeferredPayloadAtDispatch_AndCompletesOnce() {
        var context = new TopicWorkflowContext();
        context.SetValue("progress", 10);
        var activity = new EventTriggerActivity("progress", "progress.changed",
            new Lazy<object?>(() => context.GetValue<int>("progress")));
        object? received = null;
        var completions = 0;
        activity.CustomEventTriggered += (_, args) => {
            Assert.Same(context, args.Context);
            Assert.False(args.WaitForResponse);
            received = args.EventData;
        };
        activity.ActivityCompleted += (_, _) => completions++;
        context.SetValue("progress", 50);

        var result = await activity.RunAsync(context);

        Assert.Equal(50, received);
        Assert.False(result.IsWaiting);
        Assert.Equal(ActivityState.Completed, activity.CurrentState);
        Assert.Equal(1, completions);
    }

    [Fact]
    public async Task InlineHostResponse_IsDroppedBecauseWaitingStateIsSetAfterDispatch() {
        var context = new TopicWorkflowContext();
        var activity = EventTriggerActivity.CreateWaitForResponse("dialog", "confirm", "response");
        using var cancellation = new CancellationTokenSource();
        activity.CustomEventTriggered += (_, _) => activity.HandleUIResponse(context, "yes");
        var run = activity.RunAsync(context, cancellationToken: cancellation.Token);
        try {
            Assert.Equal(ActivityState.WaitingForUserInput, activity.CurrentState);
            Assert.Null(context.GetValue<string>("response"));
            Assert.False(run.IsCompleted);
        } finally {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task Cancellation_FailsActivityButLeavesWaitingMarkersInContext() {
        var context = new TopicWorkflowContext();
        var activity = EventTriggerActivity.CreateWaitForResponse("dialog", "confirm", "response");
        using var cancellation = new CancellationTokenSource();
        var run = activity.RunAsync(context, cancellationToken: cancellation.Token);
        Assert.Equal(ActivityState.WaitingForUserInput, activity.CurrentState);

        cancellation.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ActivityState.Failed, activity.CurrentState);
        Assert.Equal("confirm", context.GetValue<string>("dialog_WaitingForEvent"));
        Assert.Equal("response", context.GetValue<string>("dialog_ResponseKey"));
    }
}
