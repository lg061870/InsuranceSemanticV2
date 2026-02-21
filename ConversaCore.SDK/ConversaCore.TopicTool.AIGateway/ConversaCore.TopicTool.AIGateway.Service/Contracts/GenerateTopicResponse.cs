namespace ConversaCore.TopicTool.AIGateway.Service.Contracts;

public sealed class GenerateTopicResponse
{
    public IList<TopicPayload> Topics { get; set; } = new List<TopicPayload>();
}

public sealed class TopicPayload
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IList<ActivityPayload> Activities { get; set; } = new List<ActivityPayload>();
}

public sealed class ActivityPayload
{
    public string ActivityId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public int Order { get; set; }

    // Additional properties will mirror the VSIX-side contract but are omitted here for brevity.
}
