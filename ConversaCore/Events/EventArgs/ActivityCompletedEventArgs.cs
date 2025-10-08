// ================================
// EventArgs types
// ================================
namespace ConversaCore.Events;

public class ActivityCompletedEventArgs : EventArgs {
    public string ActivityId { get; }
    public object Context { get; }

    public ActivityCompletedEventArgs(string activityId, object ctx) {
        ActivityId = activityId;
        Context = ctx;
    }
}
