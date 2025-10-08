using ConversaCore.Context;
using ConversaCore.TopicFlow;

namespace ConversaCore.Models;

/// <summary>
/// Response from processing a chat message
/// </summary>
public class ChatResponse {
    public string Content { get; set; } = "";
    public bool IsAdaptiveCard { get; set; }
    public string? AdaptiveCardJson { get; set; }
    public List<ChatEvent> Events { get; set; } = new();
    public string? TopicName { get; set; }
    public bool UsedTopicSystem { get; set; }
    public string Message { get; internal set; }
    public TopicWorkflowContext wfContext { get; set; }
}
