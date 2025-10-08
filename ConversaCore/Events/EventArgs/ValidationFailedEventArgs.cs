// === EventArgs classes ===
namespace ConversaCore.Events;

/// <summary>
/// Event args for validation or model-binding failures.
/// </summary>
public class ValidationFailedEventArgs : EventArgs {
    public Exception Exception { get; }
    public string? CardJson { get; }

    public ValidationFailedEventArgs(Exception exception, string? cardJson = null) {
        Exception = exception;
        CardJson = cardJson;
    }
}
