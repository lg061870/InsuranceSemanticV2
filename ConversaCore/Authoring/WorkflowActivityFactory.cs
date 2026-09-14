using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Activities;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace ConversaCore.Authoring;

internal sealed class WorkflowActivityFactory : IWorkflowActivityFactory
{
    private readonly Kernel _kernel;
    private readonly TopicWorkflowContext _context;
    private readonly ILoggerFactory _loggerFactory;

    public WorkflowActivityFactory(
        Kernel kernel,
        TopicWorkflowContext context,
        ILoggerFactory loggerFactory)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public PromptActivity CreatePrompt(PromptActivityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new PromptActivity(
            definition.ActivityId,
            _kernel,
            _loggerFactory.CreateLogger<PromptActivity>())
        {
            SystemPrompt = definition.SystemPrompt,
            UserPromptTemplate = definition.UserPromptTemplate,
            Temperature = definition.Temperature,
            MaxTokens = definition.MaxTokens,
            RequireJsonOutput = definition.RequireJsonOutput,
            ModelId = definition.ModelId,
            JsonSchemaHint = definition.JsonSchemaHint
        };
    }

    public QuickAnswerActivity CreateQuickAnswer(QuickAnswerActivityDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new QuickAnswerActivity(
            definition.ActivityId,
            definition.Question,
            definition.Answers,
            _context,
            _loggerFactory.CreateLogger<QuickAnswerActivity>(),
            definition.IsRequired);
    }

    public DefinitionAdaptiveCardActivity<TModel> CreateAdaptiveCard<TModel>(
        GeneratedAdaptiveCardDefinition definition)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new DefinitionAdaptiveCardActivity<TModel>(
            definition,
            _context,
            _loggerFactory.CreateLogger<AdaptiveCardActivity<TModel>>());
    }
}
