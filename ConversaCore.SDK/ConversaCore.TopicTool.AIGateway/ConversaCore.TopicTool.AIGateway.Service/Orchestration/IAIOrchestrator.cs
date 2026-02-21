using ConversaCore.TopicTool.AIGateway.Service.Contracts;

namespace ConversaCore.TopicTool.AIGateway.Service.Orchestration;

public interface IAIOrchestrator
{
    Task<GenerateTopicResponse> GenerateAsync(GenerateTopicRequest request, CancellationToken cancellationToken);

    Task<GenerateTopicResponse> RefineAsync(RefineTopicRequest request, CancellationToken cancellationToken);
}
