namespace ConversaCore.TopicTool.AIGateway.Service.Contracts;

public sealed class RefineTopicRequest
{
    public object ExistingStructure { get; set; } = default!; // Will be aligned with VSIX TopicDesignerDocument shape

    public string? IntentText { get; set; }

    public string Version { get; set; } = "1.0";
}
