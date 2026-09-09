namespace ConversaCore.Tools;

/// <summary>Indicates a tool failure may be retried within its declared retry budget.</summary>
public sealed class ToolTransientException : Exception
{
    /// <summary>Creates a transient tool failure without including sensitive payloads.</summary>
    public ToolTransientException(string message) : base(message) { }
}
