using System.Text;
using ConversaCore.TopicTool.Models;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Builds prompts for AI-assisted topic code generation.
    /// All prompts include the embedded ConversaCore Topic Authoring Guide.
    /// </summary>
    internal static class TopicAiPromptBuilder
    {
        public static string BuildTopicCodePrompt(TopicSpec spec)
        {
            var guide = AuthoringGuideResource.GetGuideText();

            var sb = new StringBuilder();
            sb.AppendLine("SYSTEM INSTRUCTIONS:");
            sb.AppendLine("You are a ConversaCore TopicFlow code generator.");
            sb.AppendLine("Follow the ConversaCore Topic Authoring Guide strictly.");
            sb.AppendLine();
            sb.AppendLine("=== ConversaCore Topic Authoring Guide (embedded) ===");
            sb.AppendLine(guide);
            sb.AppendLine("=== End of Guide ===");
            sb.AppendLine();
            sb.AppendLine("USER REQUEST:");
            sb.AppendLine("Generate or update the .Generated.cs partial for a topic using the following spec.");
            sb.AppendLine();
            sb.AppendLine("TopicSpec JSON:");
            sb.AppendLine(Newtonsoft.Json.JsonConvert.SerializeObject(spec, Newtonsoft.Json.Formatting.Indented));

            return sb.ToString();
        }
    }
}
