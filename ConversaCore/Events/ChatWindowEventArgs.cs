using ConversaCore.TopicFlow;

namespace ConversaCore.Events;

/// <summary>
/// Event args for conversation start request from UI.
/// </summary>
public class ConversationStartRequestedEventArgs : EventArgs
{
    public CancellationToken CancellationToken { get; }
    
    public ConversationStartRequestedEventArgs(CancellationToken cancellationToken = default)
    {
        CancellationToken = cancellationToken;
    }
}

/// <summary>
/// Event args for user message received from UI.
/// </summary>
public class UserMessageReceivedEventArgs : EventArgs
{
    public string Message { get; }
    public CancellationToken CancellationToken { get; }
    
    public UserMessageReceivedEventArgs(string message, CancellationToken cancellationToken = default)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        CancellationToken = cancellationToken;
    }
}

/// <summary>
/// Event args for adaptive card submission from UI.
/// </summary>
public class CardSubmittedEventArgs : EventArgs
{
    public Dictionary<string, object> Data { get; }
    public CancellationToken CancellationToken { get; }
    
    public CardSubmittedEventArgs(Dictionary<string, object> data, CancellationToken cancellationToken = default)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        CancellationToken = cancellationToken;
    }
}

/// <summary>
/// Event args for conversation reset request from UI.
/// </summary>
public class ConversationResetRequestedEventArgs : EventArgs
{
    public CancellationToken CancellationToken { get; }
    
    public ConversationResetRequestedEventArgs(CancellationToken cancellationToken = default)
    {
        CancellationToken = cancellationToken;
    }
}
