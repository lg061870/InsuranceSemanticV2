namespace ConversaCore.Integrations.Models;

/// <summary>
/// Base request for integration calls
/// </summary>
public class IntegrationRequest<TPayload>
{
    /// <summary>
    /// Request payload
    /// </summary>
    public TPayload Payload { get; set; } = default!;

    /// <summary>
    /// Optional absolute URL to call for this request.
    /// If provided, this takes precedence over the integration's BaseUrl.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Optional headers to include
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Optional query parameters
    /// </summary>
    public Dictionary<string, string>? QueryParameters { get; set; }

    /// <summary>
    /// Timeout in seconds (defaults to 30)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Number of retry attempts on failure (defaults to 3)
    /// </summary>
    public int RetryCount { get; set; } = 3;
}
