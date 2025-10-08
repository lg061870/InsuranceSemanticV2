using ConversaCore.TopicFlow;
using System;

namespace ConversaCore.Models
{
    public enum ActivityLifecycleState
    {
        Created,
        Executing,
        Executed,
        WaitingForUserInput,
        UserInputCollected,
        Completed,
        Failed
    }

    public enum TopicLifecycleState
    {
        Created,
        Starting,
        Running,
        WaitingForUserInput,
        WaitingForSubTopic,  // NEW: Waiting for sub-topic to complete
        Resuming,
        Completed,
        Failed,
        Cancelled
    }

    public class ActivityLifecycleEventArgs : EventArgs {
        public string ActivityId { get; }
        public ActivityState State { get; }   // ✅ Not "NewState"
        public object? Data { get; }

        public ActivityLifecycleEventArgs(string activityId, ActivityState state, object? data) {
            ActivityId = activityId;
            State = state;
            Data = data;
        }
    }


    public class TopicLifecycleEventArgs : EventArgs
    {
        public string TopicName { get; }
        public TopicLifecycleState State { get; }
        public object? Data { get; }
        public TopicLifecycleEventArgs(string topicName, TopicLifecycleState state, object? data = null)
        {
            TopicName = topicName;
            State = state;
            Data = data;
        }
    }
}
