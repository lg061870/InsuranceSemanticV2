using ConversaCore.Runtime;
using ConversaCore.TopicFlow;

namespace ConversaCore.Tests.Runtime;

/// <summary>Contract coverage for the CC-300 typed output hierarchy.</summary>
public sealed class ConversationOutputContractTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StandardOutputs_CarryConversationIdentityAndImmutableSnapshots()
    {
        ConversationOutput[] outputs =
        [
            new MessageOutput("conversation-1", "Welcome", ConversationMessageRole.Assistant, OccurredAt),
            new AdaptiveCardOutput("conversation-1", "card-1", "{}",
                ConversationCardRenderMode.Replace, true, OccurredAt),
            new CardStateOutput("conversation-1", "card-1", ConversationCardState.ReadOnly, OccurredAt),
            new PromptStateOutput("conversation-1", ConversationPromptState.Disabled, "card-1", OccurredAt),
            new TopicLifecycleOutput("conversation-1", "welcome", ConversationTopicState.Running, null, OccurredAt),
            new ActivityLifecycleOutput("conversation-1", "welcome", "greeting",
                ConversationActivityState.Completed, null, OccurredAt),
            new NotificationProbe("conversation-1", "profile.updated", 1, OccurredAt),
            new InteractionProbe("conversation-1", "request-1", "appointment.confirm", 2,
                TimeSpan.FromMinutes(1), OccurredAt)
        ];

        Assert.All(outputs, output =>
        {
            Assert.Equal("conversation-1", output.ConversationId);
            Assert.Equal(OccurredAt, output.OccurredAtUtc);
            Assert.DoesNotContain(output.GetType().GetProperties(), property =>
                property.SetMethod?.IsPublic == true);
        });
    }

    [Fact]
    public void OutputContracts_DoNotExposeMutableWorkflowContext()
    {
        var outputTypes = typeof(ConversationOutput).Assembly.GetTypes()
            .Where(type => typeof(ConversationOutput).IsAssignableFrom(type));

        Assert.DoesNotContain(outputTypes.SelectMany(type => type.GetProperties()), property =>
            typeof(TopicWorkflowContext).IsAssignableFrom(property.PropertyType));
    }

    [Fact]
    public void InvalidRequiredIdentityOrPayload_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new MessageOutput(" ", "message"));
        Assert.Throws<ArgumentException>(() => new MessageOutput("conversation-1", " "));
        Assert.Throws<ArgumentException>(() => new AdaptiveCardOutput("conversation-1", " ", "{}"));
        Assert.Throws<ArgumentException>(() => new AdaptiveCardOutput("conversation-1", "card-1", " "));
        Assert.Throws<ArgumentException>(() =>
            new ActivityLifecycleOutput("conversation-1", "topic-1", " ", ConversationActivityState.Running));
        Assert.Throws<ArgumentException>(() => new NotificationProbe("conversation-1", " ", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new InteractionProbe("conversation-1", "request-1", "interaction", 1, TimeSpan.Zero));
    }

    [Fact]
    public void HostOutputBases_ValidateVersionCorrelationAndTimeout()
    {
        var notification = new NotificationProbe("conversation-1", "profile.updated", 3);
        var request = new InteractionProbe("conversation-1", "request-9", "appointment.confirm", 2,
            TimeSpan.FromSeconds(30));

        Assert.Equal(("profile.updated", 3), (notification.EventName, notification.Version));
        Assert.Equal(("request-9", "appointment.confirm", 2, TimeSpan.FromSeconds(30)),
            (request.RequestId, request.InteractionName, request.Version, request.Timeout));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NotificationProbe("conversation-1", "event", 0));
        Assert.Throws<ArgumentException>(() =>
            new InteractionProbe("conversation-1", " ", "interaction", 1, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void SubscriptionContract_StreamsOnlyTypedConversationOutputs()
    {
        var method = typeof(IConversationOutputSubscription).GetMethod(
            nameof(IConversationOutputSubscription.ReadAllAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(IAsyncEnumerable<ConversationOutput>), method!.ReturnType);
    }

    private sealed record NotificationProbe : HostNotificationOutput
    {
        public NotificationProbe(string conversationId, string eventName, int version,
            DateTimeOffset? occurredAtUtc = null) : base(conversationId, eventName, version, occurredAtUtc) { }
    }

    private sealed record InteractionProbe : HostInteractionRequestOutput
    {
        public InteractionProbe(string conversationId, string requestId, string interactionName, int version,
            TimeSpan timeout, DateTimeOffset? occurredAtUtc = null)
            : base(conversationId, requestId, interactionName, version, timeout, occurredAtUtc) { }
    }
}
