using ConversaCore.TopicFlow;

namespace ConversaCore.Events;

/// <summary>
/// Event arguments for custom events triggered by EventTriggerActivity.
/// </summary>
/// <remarks>
/// Deprecated in favor of typed host notification and host interaction output envelopes observed via <c>IConversationRuntime</c>.
/// </remarks>
[Obsolete("CustomEventTriggeredEventArgs is deprecated. Use typed host notification or host interaction output envelopes with IConversationRuntime instead.")]
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
