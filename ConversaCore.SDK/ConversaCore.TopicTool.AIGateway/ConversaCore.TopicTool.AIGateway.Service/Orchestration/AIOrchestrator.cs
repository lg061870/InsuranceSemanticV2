using ConversaCore.TopicTool.AIGateway.Service.Configuration;
using ConversaCore.TopicTool.AIGateway.Service.Contracts;
using ConversaCore.TopicTool.AIGateway.Service.Providers;
using ConversaCore.TopicTool.AIGateway.Service.Validation;

namespace ConversaCore.TopicTool.AIGateway.Service.Orchestration;

public sealed class AIOrchestrator : IAIOrchestrator
{
    private readonly IPromptBuilder _promptBuilder;
    private readonly ProviderFactory _providerFactory;
    private readonly IJsonSchemaValidator _schemaValidator;
    private readonly GatewaySettings _settings;

    public AIOrchestrator(
        IPromptBuilder promptBuilder,
        ProviderFactory providerFactory,
        IJsonSchemaValidator schemaValidator,
        GatewaySettings settings)
    {
        _promptBuilder = promptBuilder;
        _providerFactory = providerFactory;
        _schemaValidator = schemaValidator;
        _settings = settings;
    }

    public async Task<GenerateTopicResponse> GenerateAsync(GenerateTopicRequest request, CancellationToken cancellationToken)
    {
        // Build prompt
        var prompt = _promptBuilder.BuildGeneratePrompt(request);

        // Select provider and send request (implementation is currently stubbed)
        var adapter = _providerFactory.Create();
        var rawJson = await adapter.SendAsync(new Providers.LLMRequest
        {
            Model = _settings.Providers.FirstOrDefault(p => p.Name == _settings.DefaultProvider)?.Model ?? string.Empty,
            Prompt = prompt
        }, cancellationToken).ConfigureAwait(false);

        // Validate JSON structure (stubbed)
        if (!_schemaValidator.IsValid(rawJson))
        {
            throw new InvalidOperationException("LLM response did not pass JSON schema validation.");
        }

        // TODO: Deserialize rawJson into GenerateTopicResponse once schema is finalized.
        return new GenerateTopicResponse();
    }

    public async Task<GenerateTopicResponse> RefineAsync(RefineTopicRequest request, CancellationToken cancellationToken)
    {
        var prompt = _promptBuilder.BuildRefinePrompt(request);

        var adapter = _providerFactory.Create();
        var rawJson = await adapter.SendAsync(new Providers.LLMRequest
        {
            Model = _settings.Providers.FirstOrDefault(p => p.Name == _settings.DefaultProvider)?.Model ?? string.Empty,
            Prompt = prompt
        }, cancellationToken).ConfigureAwait(false);

        if (!_schemaValidator.IsValid(rawJson))
        {
            throw new InvalidOperationException("LLM response did not pass JSON schema validation.");
        }

        return new GenerateTopicResponse();
    }
}
