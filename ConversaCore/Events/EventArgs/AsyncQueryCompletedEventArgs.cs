using ConversaCore.Interfaces;

namespace ConversaCore.TopicFlow;


public class AsyncQueryCompletedEventArgs : EventArgs {
    public TopicWorkflowContext Context { get; }
    public object? Output { get; }
    public string QueryId { get; }
    public TopicFlowActivity? Activity { get; }  // ✅ NEW

    public AsyncQueryCompletedEventArgs(
        TopicWorkflowContext context,
        object? output,
        string queryId,
        TopicFlowActivity? activity = null) {
        Context = context;
        Output = output;
        QueryId = queryId;
        Activity = activity;
    }
}

