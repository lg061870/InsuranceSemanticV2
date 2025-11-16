using ConversaCore.TopicFlow;

namespace ConversaCore.Interfaces; 

/// <summary>
/// Marker interface for async-notifiable activities.
/// </summary>
public interface IAsyncNotifiableActivity {
    event EventHandler<AsyncQueryCompletedEventArgs>? AsyncCompleted;
}