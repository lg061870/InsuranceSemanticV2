namespace ConversaCore.TopicTool.AIGateway.Service.Providers;

public sealed class OpenAIProviderAdapter : IProviderAdapter
{
    public Task<string> SendAsync(LLMRequest request, CancellationToken cancellationToken)
    {
        // TODO: Implement real OpenAI HTTP call using API key from secure storage.
        // For now, this is a stub that throws to make missing configuration obvious.
        throw new NotImplementedException("OpenAI provider adapter is not yet implemented.");
    }
}