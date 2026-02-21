using System.Text;
using ConversaCore.TopicTool.Models;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Builds DI registration snippets for generated topics.
    /// This does not modify any files; it only returns text for copy/paste.
    /// </summary>
    internal static class TopicRegistrationSnippetBuilder
    {
        public static string BuildRegistrationSnippet(TopicSpec spec)
        {
            var ns = string.IsNullOrWhiteSpace(spec.Namespace) ? "YourApp.Topics" : spec.Namespace.Trim();
            var className = string.Concat(spec.Name.Trim(), "Topic");

            var sb = new StringBuilder();
            sb.AppendLine("// ConversaCore topic registration");
            sb.AppendLine($"using {ns};");
            sb.AppendLine();
            sb.AppendLine("// In your Program.cs or topic registration module:");
            sb.AppendLine("services.AddScoped<ITopic>(sp => new " + className + "(" +
                          "sp.GetRequiredService<TopicWorkflowContext>()," +
                          " sp.GetRequiredService<ILogger<" + className + ">>()," +
                          " sp.GetRequiredService<IConversationContext>()));");
            return sb.ToString();
        }
    }
}
