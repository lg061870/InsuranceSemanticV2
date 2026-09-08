using ConversaCore.Context;
using ConversaCore.Runtime;

namespace ConversaCore.Tests.Runtime;

/// <summary>Contract and dispatch coverage for typed host notifications (CC-303).</summary>
public sealed class HostNotificationTests
{
    [Fact]
    public void Construction_FreezesPayloadAndPreservesStableIdentity()
    {
        var source = new MutablePayload { Name = "original", Values = [1, 2] };
        var notification = new HostNotification<MutablePayload>(
            "conversation-1", "appointment.updated", 2, source);

        source.Name = "changed";
        source.Values.Add(3);
        var firstRead = notification.Payload;
        firstRead.Name = "also changed";
        firstRead.Values.Clear();
        var secondRead = notification.Payload;

        Assert.IsAssignableFrom<HostNotificationOutput>(notification);
        Assert.Equal(("appointment.updated", 2), (notification.EventName, notification.Version));
        Assert.Equal("original", secondRead.Name);
        Assert.Equal([1, 2], secondRead.Values);
        Assert.Equal("original", notification.PayloadSnapshot.GetProperty("Name").GetString());
    }

    [Fact]
    public void Construction_RejectsInvalidIdentityVersionAndNullPayload()
    {
        Assert.Throws<ArgumentException>(() =>
            new HostNotification<string>("conversation-1", " ", 1, "payload"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HostNotification<string>("conversation-1", "event", 0, "payload"));
        Assert.Throws<ArgumentNullException>(() =>
            new HostNotification<MutablePayload>("conversation-1", "event", 1, null!));
        Assert.Throws<NotSupportedException>(() =>
            new HostNotification<Action>("conversation-1", "event", 1, () => { }));
    }

    [Fact]
    public async Task Dispatcher_PublishesNotificationWithoutWaitingForConsumer()
    {
        var session = new ConversationSession(new ConversationContext("conversation-1", "subject-1"));
        await using var dispatcher = new ConversationOutputDispatcher(session);
        var subscription = dispatcher.Subscribe();
        var notification = new HostNotification<MutablePayload>(
            "conversation-1", "appointment.updated", 1, new MutablePayload { Name = "Ada" });

        var publication = dispatcher.DispatchAsync(notification);

        Assert.True(publication.IsCompletedSuccessfully);
        await dispatcher.DisposeAsync();
        var outputs = new List<ConversationOutput>();
        await foreach (var output in subscription.ReadAllAsync()) outputs.Add(output);
        Assert.Same(notification, Assert.Single(outputs));
    }

    public sealed class MutablePayload
    {
        public string Name { get; set; } = string.Empty;
        public List<int> Values { get; set; } = [];
    }
}
