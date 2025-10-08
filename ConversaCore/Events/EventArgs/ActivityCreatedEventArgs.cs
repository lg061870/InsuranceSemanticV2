// ================================
// EventArgs types
// ================================
using ConversaCore.TopicFlow;

namespace ConversaCore.Events;

public class ActivityCreatedEventArgs : EventArgs {
    public string ActivityId { get; }
    public object? Content { get; }
    public TopicWorkflowContext Context { get; }

    public ActivityCreatedEventArgs(string activityId, object? content, TopicWorkflowContext ctx) {
        ActivityId = activityId;
        Content = content;
        Context = ctx;
    }
}
