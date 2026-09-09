using ConversaCore.Runtime;
using ConversaCore.UI.Models;

namespace ConversaCore.Tests.UI;

public sealed class ConversationOutputViewStateTests
{
    [Fact]
    public void StandardOutputs_ProjectOrderedMessagesCardsAndPromptState()
    {
        var state = new ConversationOutputViewState();

        var messageChange = state.Apply(new MessageOutput("conversation-1", "Welcome"));
        var cardChange = state.Apply(new AdaptiveCardOutput(
            "conversation-1", "contact", "{\"version\":1}", isInputRequired: true));
        var promptChange = state.Apply(new PromptStateOutput(
            "conversation-1", ConversationPromptState.Disabled, "contact"));
        state.Apply(new AdaptiveCardOutput(
            "conversation-1", "contact", "{\"version\":2}", ConversationCardRenderMode.Replace));

        Assert.True(messageChange.ClearBusyIndicator);
        Assert.True(cardChange.ClearBusyIndicator);
        Assert.True(promptChange.StateChanged);
        Assert.False(state.IsPromptEnabled);
        Assert.Collection(state.Messages,
            message => Assert.Equal("Welcome", message.Content),
            card =>
            {
                Assert.True(card.IsAdaptiveCard);
                Assert.Equal("contact", card.CardId);
                Assert.Equal("{\"version\":2}", card.AdaptiveCardJson);
            });
    }

    [Fact]
    public void CardState_UpdatesOnlyMatchingCardAndRequestsScrollWhenActivated()
    {
        var state = new ConversationOutputViewState();
        state.Apply(new AdaptiveCardOutput("conversation-1", "first", "{}"));
        state.Apply(new AdaptiveCardOutput("conversation-1", "second", "{}"));

        var disabled = state.Apply(new CardStateOutput(
            "conversation-1", "first", ConversationCardState.ReadOnly));
        var enabled = state.Apply(new CardStateOutput(
            "conversation-1", "first", ConversationCardState.Active));

        Assert.True(disabled.StateChanged);
        Assert.False(disabled.ScrollToBottom);
        Assert.True(enabled.ScrollToBottom);
        Assert.All(state.Messages, card => Assert.True(card.IsActive));
    }

    [Fact]
    public void LifecycleAndHostOutputs_DoNotLeakIntoStandardTranscript()
    {
        var state = new ConversationOutputViewState();

        var lifecycle = state.Apply(new TopicLifecycleOutput(
            "conversation-1", "start", ConversationTopicState.Running));
        var host = state.Apply(new HostNotification<string>(
            "conversation-1", "site.panel.show", 1, "customer"));

        Assert.Equal(ConversationOutputChange.None, lifecycle);
        Assert.Equal(ConversationOutputChange.None, host);
        Assert.Empty(state.Messages);
    }

    [Fact]
    public async Task HostOutputContext_ForwardsTypedCorrelatedResponse()
    {
        var runtime = new RecordingRuntime();
        var request = new HostInteractionRequest<string, bool>(
            "conversation-1", "request-1", "site.confirm", 1, TimeSpan.FromSeconds(5), "Continue?");
        var context = new ConversationHostOutputContext(request, runtime);

        await context.RespondAsync(true);

        Assert.True(context.RequiresResponse);
        var response = Assert.IsType<HostInteractionResponse<bool>>(runtime.Response);
        Assert.Equal("request-1", response.RequestId);
        Assert.True(response.Payload);
    }

    [Fact]
    public async Task HostOutputContext_RejectsResponseToOneWayNotification()
    {
        var runtime = new RecordingRuntime();
        HostOutput notification = new HostNotification<string>(
            "conversation-1", "site.panel.show", 1, "customer");
        var context = new ConversationHostOutputContext(notification, runtime);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.RespondAsync("not allowed"));

        Assert.False(context.RequiresResponse);
        Assert.Null(runtime.Response);
    }

    private sealed class RecordingRuntime : IConversationRuntime
    {
        public string ConversationId => "conversation-1";
        public HostInteractionResponse? Response { get; private set; }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendMessageAsync(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SubmitCardAsync(CardSubmission submission, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RespondToHostInteractionAsync(HostInteractionResponse response,
            CancellationToken cancellationToken = default)
        {
            Response = response;
            return Task.CompletedTask;
        }
        public Task ResetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IConversationOutputSubscription Subscribe() => throw new NotSupportedException();
    }
}
