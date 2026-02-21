namespace ConversaCore.TopicTool.AIGateway.Service.Providers;

public sealed class AzureOpenAIProviderAdapter : IProviderAdapter
{
    public Task<string> SendAsync(LLMRequest request, CancellationToken cancellationToken)
    {
        // TODO: Implement real Azure OpenAI HTTP call using API key from secure storage.
        throw new NotImplementedException("Azure OpenAI provider adapter is not yet implemented.");
    }
}