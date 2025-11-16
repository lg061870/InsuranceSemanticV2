using ConversaCore.TopicFlow;

namespace ConversaCore.Events;

/// <summary>
/// Event arguments for custom events triggered by EventTriggerActivity.
/// </summary>
public class CustomEventTriggeredEventArgs : EventArgs {
    public string EventName { get; }
    public object? EventData { get; }
    public TopicWorkflowContext Context { get; }
    public bool WaitForResponse { get; }

    public CustomEventTriggeredEventArgs(string eventName, object? eventData, TopicWorkflowContext context, bool waitForResponse) {
        EventName = eventName;
        EventData = eventData;
        Context = context;
        WaitForResponse = waitForResponse;
    }
}
