namespace ConversaCore.Integrations.Models;

/// <summary>
/// Response from integration call
/// </summary>
public class IntegrationResponse<TPayload>
{
    /// <summary>
    /// Success status
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Response payload
    /// </summary>
    public TPayload? Data { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// HTTP status code
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Response headers
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Number of retry attempts made
    /// </summary>
    public int RetryAttempts { get; set; }

    /// <summary>
    /// Raw JSON response body (when available).
    /// This is useful for debugging or when the remote schema doesn't
    /// cleanly map onto a typed payload model.
    /// </summary>
    public string? RawBody { get; set; }
}
