namespace ConversaCore.TopicTool.AIGateway.Service.Providers;

public sealed class LLMRequest
{
    public string Model { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;
}