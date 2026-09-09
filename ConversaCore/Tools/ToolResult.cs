namespace ConversaCore.Tools;

/// <summary>Immutable typed result of a tool invocation.</summary>
/// <typeparam name="TResult">The successful result value type.</typeparam>
public sealed record ToolResult<TResult>
{
    private ToolResult(bool succeeded, TResult? value, string? errorCode, string? errorMessage)
    {
        Succeeded = succeeded;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>Gets whether the operation completed successfully.</summary>
    public bool Succeeded { get; }

    /// <summary>Gets the typed value, or the default value when the operation failed.</summary>
    public TResult? Value { get; }

    /// <summary>Gets a stable non-sensitive failure code, when failed.</summary>
    public string? ErrorCode { get; }

    /// <summary>Gets a safe diagnostic message, when failed. Payloads must not be included.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Creates a successful result.</summary>
    public static ToolResult<TResult> Success(TResult value) =>
        new(true, value, null, null);

    /// <summary>Creates a failed result with a stable code and safe diagnostic message.</summary>
    public static ToolResult<TResult> Failure(string errorCode, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("Error code must not be null, empty, or whitespace.", nameof(errorCode));
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message must not be null, empty, or whitespace.", nameof(errorMessage));
        return new(false, default, errorCode.Trim(), errorMessage.Trim());
    }
}
