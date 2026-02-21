using ConversaCore.TopicTool.AIGateway.Service.Configuration;

namespace ConversaCore.TopicTool.AIGateway.Service.Providers;

public sealed class ProviderFactory
{
    private readonly GatewaySettings _settings;

    public ProviderFactory(GatewaySettings settings)
    {
        _settings = settings;
    }

    public IProviderAdapter Create(string? providerName = null)
    {
        var name = string.IsNullOrWhiteSpace(providerName) ? _settings.DefaultProvider : providerName;

        // Very simple mapping based on ProviderSettings.Type; can be extended.
        var config = _settings.Providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (config == null)
        {
            throw new InvalidOperationException($"No provider configuration found for '{name}'.");
        }

        return config.Type switch
        {
            "OpenAI" => new OpenAIProviderAdapter(),
            "AzureOpenAI" => new AzureOpenAIProviderAdapter(),
            _ => throw new InvalidOperationException($"Unknown provider type '{config.Type}'.")
        };
    }
}