using System.Text;
using ConversaCore.TopicTool.AIGateway.Service.Contracts;

namespace ConversaCore.TopicTool.AIGateway.Service.Orchestration;

public sealed class PromptBuilder : IPromptBuilder
{
    public string BuildGeneratePrompt(GenerateTopicRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an assistant that designs ConversaCore dialog topics.");
        sb.AppendLine("Return ONLY valid JSON that matches the expected schema.");
        sb.AppendLine("No markdown, no commentary, no prose.");
        sb.AppendLine();
        sb.AppendLine("User intent:");
        sb.AppendLine(request.IntentText ?? string.Empty);
        return sb.ToString();
    }

    public string BuildRefinePrompt(RefineTopicRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are refining an existing ConversaCore topic structure.");
        sb.AppendLine("Return ONLY valid JSON that matches the expected schema.");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(request.IntentText))
        {
            sb.AppendLine("Refinement notes:");
            sb.AppendLine(request.IntentText);
        }
        return sb.ToString();
    }
}
