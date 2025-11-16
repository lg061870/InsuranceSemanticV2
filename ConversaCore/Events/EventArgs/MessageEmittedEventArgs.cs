using ConversaCore.TopicFlow;

namespace ConversaCore.Events;

    /// <summary>
    /// Event arguments for when a message is emitted during topic workflow execution.
    /// </summary> 
public class MessageEmittedEventArgs : EventArgs {
    public string ActivityId { get; }
    public string Message { get; }
    public TopicWorkflowContext Context { get; }

    public MessageEmittedEventArgs(string activityId, string message, TopicWorkflowContext context) {
        ActivityId = activityId;
        Message = message;
        Context = context;
    }
}
