namespace ConversaCore.Integrations.Models;

/// <summary>
/// Configuration for an integration
/// </summary>
public class IntegrationConfiguration
{
    /// <summary>
    /// Whether this integration is enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Base URL for the integration API
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// API key or token (for pass-through, this comes from customer context)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Additional settings specific to this integration
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();

    /// <summary>
    /// Connection type: CustomerProvided, OAuth, StripeConnect
    /// </summary>
    public string ConnectionType { get; set; } = "CustomerProvided";
}

/// <summary>
/// Root configuration for all integrations
/// </summary>
public class IntegrationsConfiguration
{
    public Dictionary<string, IntegrationConfiguration> Integrations { get; set; } = new();
}
