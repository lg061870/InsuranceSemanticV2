
using ConversaCore.TopicFlow;

namespace ConversaCore.Events; 

public class TopicInsertedEventArgs : EventArgs {
    public string TopicName { get; }
    public TopicWorkflowContext Context { get; }

    public TopicInsertedEventArgs(string topicName, TopicWorkflowContext ctx) {
        TopicName = topicName;
        Context = ctx;
    }
}