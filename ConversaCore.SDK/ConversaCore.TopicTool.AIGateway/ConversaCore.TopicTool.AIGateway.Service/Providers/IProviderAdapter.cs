namespace ConversaCore.TopicTool.AIGateway.Service.Providers;

public interface IProviderAdapter
{
    Task<string> SendAsync(LLMRequest request, CancellationToken cancellationToken);
}