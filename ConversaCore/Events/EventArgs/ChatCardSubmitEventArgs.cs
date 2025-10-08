// === EventArgs classes ===
namespace ConversaCore.Events;

public class ChatCardSubmitEventArgs : EventArgs {
    public Dictionary<string, object> Data { get; }
    public ChatCardSubmitEventArgs(Dictionary<string, object> data) => Data = data;
}
