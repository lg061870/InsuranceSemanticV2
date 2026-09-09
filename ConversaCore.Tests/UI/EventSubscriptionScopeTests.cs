using ConversaCore.UI.Lifecycle;

namespace ConversaCore.Tests.UI;

public sealed class EventSubscriptionScopeTests
{
    [Fact]
    public void Dispose_DetachesEveryHandlerAndIsIdempotent()
    {
        var publisher = new Publisher();
        var calls = 0;
        var subscriptions = new EventSubscriptionScope();
        EventHandler<EventArgs> handler = (_, _) => calls++;
        subscriptions.Add<EventArgs>(
            value => publisher.Published += value,
            value => publisher.Published -= value,
            handler);

        publisher.Publish();
        subscriptions.Dispose();
        subscriptions.Dispose();
        publisher.Publish();

        Assert.Equal(1, calls);
    }

    private sealed class Publisher
    {
        public event EventHandler<EventArgs>? Published;
        public void Publish() => Published?.Invoke(this, EventArgs.Empty);
    }
}
