using System.Threading;
using System.Threading.Tasks;
using ConversaCore.TopicTool.Models;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Temporary stub implementation that just returns the constructed prompt.
    /// A real implementation would call an LLM endpoint with this prompt.
    /// </summary>
    internal sealed class StubTopicAiGenerator : ITopicAiGenerator
    {
        public Task<string> GenerateTopicCodeAsync(TopicSpec spec, CancellationToken cancellationToken)
        {
            var prompt = TopicAiPromptBuilder.BuildTopicCodePrompt(spec);
            // For now, return the prompt so it can be inspected in tooling.
            return Task.FromResult(prompt);
        }
    }
}
