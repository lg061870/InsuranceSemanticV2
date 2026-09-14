namespace ConversaCore.Authoring;

/// <summary>Immutable generated-source configuration for a prompt activity.</summary>
public sealed class PromptActivityDefinition
{
    public const int MaximumIdLength = 128;
    public const int MaximumPromptLength = 32_768;
    public const int MaximumModelIdLength = 256;
    public const int MaximumJsonSchemaHintLength = 32_768;

    public PromptActivityDefinition(
        string activityId,
        string? systemPrompt = null,
        string? userPromptTemplate = null,
        float temperature = 0.7f,
        int maxTokens = 2_048,
        bool requireJsonOutput = false,
        string modelId = "gpt-4o-mini",
        string? jsonSchemaHint = null)
    {
        ActivityId = ValidateRequired(activityId, nameof(activityId), MaximumIdLength);
        SystemPrompt = ValidateOptional(systemPrompt, nameof(systemPrompt), MaximumPromptLength);
        UserPromptTemplate = ValidateOptional(userPromptTemplate, nameof(userPromptTemplate), MaximumPromptLength);

        if (string.IsNullOrEmpty(SystemPrompt) && string.IsNullOrEmpty(UserPromptTemplate))
            throw new ArgumentException("A system prompt or user prompt template is required.", nameof(systemPrompt));
        if (float.IsNaN(temperature) || float.IsInfinity(temperature) || temperature is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(temperature), "Temperature must be between 0 and 2.");
        if (maxTokens is < 1 or > 128_000)
            throw new ArgumentOutOfRangeException(nameof(maxTokens), "Max tokens must be between 1 and 128000.");

        Temperature = temperature;
        MaxTokens = maxTokens;
        RequireJsonOutput = requireJsonOutput;
        ModelId = ValidateRequired(modelId, nameof(modelId), MaximumModelIdLength);
        JsonSchemaHint = ValidateOptional(jsonSchemaHint, nameof(jsonSchemaHint), MaximumJsonSchemaHintLength);
    }

    public string ActivityId { get; }
    public string SystemPrompt { get; }
    public string UserPromptTemplate { get; }
    public float Temperature { get; }
    public int MaxTokens { get; }
    public bool RequireJsonOutput { get; }
    public string ModelId { get; }
    public string? JsonSchemaHint { get; }

    private static string ValidateRequired(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }

    private static string ValidateOptional(string? value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        if (value.Length > maximumLength)
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        return value;
    }
}
