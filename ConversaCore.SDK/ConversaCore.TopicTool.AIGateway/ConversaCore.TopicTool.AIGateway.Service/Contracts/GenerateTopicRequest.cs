namespace ConversaCore.TopicTool.AIGateway.Service.Contracts;

public sealed class GenerateTopicRequest
{
    public string IntentText { get; set; } = string.Empty;

    public GenerateTopicSettings Settings { get; set; } = new();

    public string Version { get; set; } = "1.0";
}

public sealed class GenerateTopicSettings
{
    public bool StrictConversaCoreMode { get; set; } = true;

    public bool AllowApiActivities { get; set; }

    public bool AllowAdaptiveCardActivities { get; set; }
}
