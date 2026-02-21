using ConversaCore.TopicTool.AIGateway.Service.Contracts;

namespace ConversaCore.TopicTool.AIGateway.Service.Orchestration;

public interface IPromptBuilder
{
    string BuildGeneratePrompt(GenerateTopicRequest request);

    string BuildRefinePrompt(RefineTopicRequest request);
}