namespace ConversaCore.Events;

/// <summary>
/// Event args when no topic could handle the message.
/// </summary>
public class UnhandledMessageEventArgs : EventArgs {
    public string UserMessage { get; }

    public UnhandledMessageEventArgs(string userMessage) {
        UserMessage = userMessage;
    }
}
