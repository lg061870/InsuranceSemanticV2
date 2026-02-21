namespace ConversaCore.TopicDesigner.App;

public enum AiProvider
{
    OpenAI,
    Groq,
    Gemini,
    AzureOpenAI
}

public sealed class ProviderInfo
{
    public AiProvider Id { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string ApiKeyEnvVar { get; init; } = string.Empty;

    public string? ExtraEnvVarHint { get; init; }

    public string DocsUrl { get; init; } = string.Empty;

    public static IReadOnlyList<ProviderInfo> All { get; } = new List<ProviderInfo>
    {
        new()
        {
            Id = AiProvider.OpenAI,
            DisplayName = "OpenAI (GPT-4o)",
            ApiKeyEnvVar = "OPENAI_API_KEY",
            DocsUrl = "https://platform.openai.com/docs/quickstart"
        },
        new()
        {
            Id = AiProvider.Groq,
            DisplayName = "Groq (GROK)",
            ApiKeyEnvVar = "GROQ_API_KEY",
            DocsUrl = "https://console.groq.com/docs" 
        },
        new()
        {
            Id = AiProvider.Gemini,
            DisplayName = "Google Gemini",
            ApiKeyEnvVar = "GEMINI_API_KEY",
            DocsUrl = "https://ai.google.dev/gemini-api/docs/get-started"
        },
        new()
        {
            Id = AiProvider.AzureOpenAI,
            DisplayName = "Azure OpenAI",
            ApiKeyEnvVar = "AZURE_OPENAI_API_KEY",
            ExtraEnvVarHint = "You also need AZURE_OPENAI_ENDPOINT for your Azure resource.",
            DocsUrl = "https://learn.microsoft.com/azure/ai-services/openai/"
        }
    };
}
