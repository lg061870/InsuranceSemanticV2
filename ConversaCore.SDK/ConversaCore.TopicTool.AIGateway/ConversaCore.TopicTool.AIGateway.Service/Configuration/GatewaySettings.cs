namespace ConversaCore.TopicTool.AIGateway.Service.Configuration;

public sealed class GatewaySettings
{
    public string DefaultProvider { get; set; } = "openai";

    public IList<ProviderSettings> Providers { get; set; } = new List<ProviderSettings>();
}

public sealed class ProviderSettings
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}
