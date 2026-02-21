using System.Threading;
using System.Threading.Tasks;
using ConversaCore.TopicTool.Models;

namespace ConversaCore.TopicTool.Services
{
    /// <summary>
    /// Abstraction for AI-based topic code generation.
    /// Implementations must use TopicAiPromptBuilder so that the authoring guide is always included.
    /// </summary>
    internal interface ITopicAiGenerator
    {
        Task<string> GenerateTopicCodeAsync(TopicSpec spec, CancellationToken cancellationToken);
    }
}
