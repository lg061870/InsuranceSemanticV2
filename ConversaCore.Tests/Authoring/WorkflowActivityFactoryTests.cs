using ConversaCore.Authoring;
using ConversaCore.Context;
using ConversaCore.Events;
using ConversaCore.Registration;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ConversaCore.Tests.Authoring;

public sealed class WorkflowActivityFactoryTests
{
    [Fact]
    public void PromptDefinition_ValidatesAndNormalizesConfiguration()
    {
        var definition = new PromptActivityDefinition(
            " prompt ",
            systemPrompt: "System",
            userPromptTemplate: "Question: {input}",
            temperature: 0.25f,
            maxTokens: 512,
            requireJsonOutput: true,
            modelId: "test-model",
            jsonSchemaHint: "{}");

        Assert.Equal("prompt", definition.ActivityId);
        Assert.Equal("System", definition.SystemPrompt);
        Assert.Equal("Question: {input}", definition.UserPromptTemplate);
        Assert.Equal(0.25f, definition.Temperature);
        Assert.Equal(512, definition.MaxTokens);
        Assert.True(definition.RequireJsonOutput);
        Assert.Equal("test-model", definition.ModelId);
        Assert.Equal("{}", definition.JsonSchemaHint);

        Assert.Throws<ArgumentException>(() => new PromptActivityDefinition("id"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptActivityDefinition("id", "prompt", temperature: 2.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PromptActivityDefinition("id", "prompt", maxTokens: 0));
    }

    [Fact]
    public void QuickAnswerDefinition_CopiesAndValidatesAnswers()
    {
        var source = new List<string> { " Yes ", "No" };
        var definition = new QuickAnswerActivityDefinition(" choice ", " Continue? ", source, true);
        source[0] = "mutated";

        Assert.Equal("choice", definition.ActivityId);
        Assert.Equal("Continue?", definition.Question);
        Assert.Equal(["Yes", "No"], definition.Answers);
        Assert.True(definition.IsRequired);

        Assert.Throws<ArgumentException>(() =>
            new QuickAnswerActivityDefinition("id", "question", []));
        Assert.Throws<ArgumentException>(() =>
            new QuickAnswerActivityDefinition("id", "question", ["same", "SAME"]));
    }

    [Fact]
    public void Factory_MapsPromptDefinition_AndReturnsFreshActivities()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>();
        var definition = new PromptActivityDefinition(
            "prompt", "System", "Input: {input}", 0.4f, 777, true, "model", "schema");

        var first = factory.CreatePrompt(definition);
        var second = factory.CreatePrompt(definition);

        Assert.NotSame(first, second);
        Assert.Equal("prompt", first.Id);
        Assert.Equal("System", first.SystemPrompt);
        Assert.Equal("Input: {input}", first.UserPromptTemplate);
        Assert.Equal(0.4f, first.Temperature);
        Assert.Equal(777, first.MaxTokens);
        Assert.True(first.RequireJsonOutput);
        Assert.Equal("model", first.ModelId);
        Assert.Equal("schema", first.JsonSchemaHint);
    }

    [Fact]
    public async Task Factory_MapsQuickAnswerDefinition_ToStandardCardLifecycle()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var factory = scope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>();
        var activity = factory.CreateQuickAnswer(
            new QuickAnswerActivityDefinition("answer", "Continue?", ["Yes", "No"], true));
        string? emittedJson = null;
        activity.CardJsonSent += (_, args) => emittedJson = args.CardJson;

        var result = await activity.RunAsync(context, null, CancellationToken.None);

        Assert.True(result.IsWaiting);
        Assert.True(activity.IsRequired);
        Assert.Equal("answer", activity.ModelContextKey);
        Assert.NotNull(emittedJson);
        Assert.Contains("Continue?", emittedJson, StringComparison.Ordinal);
        Assert.Contains("Yes", emittedJson, StringComparison.Ordinal);
        Assert.Contains("No", emittedJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registration_IsIdempotentScoped_AndDoesNotShareConversationState()
    {
        using var provider = BuildProvider(registerTwice: true);
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var firstFactory = firstScope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>();
        var secondFactory = secondScope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();

        Assert.NotSame(firstFactory, secondFactory);
        Assert.NotSame(firstContext, secondContext);
        Assert.Same(firstFactory, firstScope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>());

        var activity = firstFactory.CreateQuickAnswer(
            new QuickAnswerActivityDefinition("answer", "Continue?", ["Yes", "No"]));
        await activity.RunAsync(firstContext, null, CancellationToken.None);
        activity.OnInputCollected(new AdaptiveCardInputCollectedEventArgs(
            new Dictionary<string, object> { ["answer"] = "Yes" }));

        Assert.NotNull(firstContext.GetValue<Dictionary<string, object>>("answer"));
        Assert.Null(secondContext.GetValue<Dictionary<string, object>>("answer"));
    }

    [Fact]
    public async Task GeneratedStyleTopic_ComposesWithExplicitlyInjectedFactory()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var topic = ActivatorUtilities.CreateInstance<GeneratedStyleTopic>(scope.ServiceProvider);
        await topic.InitializeAsync();

        var activities = topic.GetAllActivities().ToArray();
        Assert.Equal(2, activities.Length);
        Assert.Equal("welcome", activities[0].Id);
        Assert.Equal("next", activities[1].Id);
    }

    private static ServiceProvider BuildProvider(bool registerTwice = false)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton<IChatCompletionService>(new FakeChatCompletionService());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(kernelBuilder.Build());
        services.AddScoped<IConversationContext>(_ => new ConversationContext(
            Guid.NewGuid().ToString(), "subject", NullLogger<ConversationContext>.Instance));
        services.AddScoped<TopicWorkflowContext>();

        var builder = new ConversaCoreBuilder(services);
        builder.AddConversationRuntimeFoundation();
        if (registerTwice)
            builder.AddConversationRuntimeFoundation();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private sealed class FakeChatCompletionService : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes { get; } =
            new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatMessageContent>>(
                [new ChatMessageContent(AuthorRole.Assistant, "ok")]);

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class GeneratedStyleTopic : ComposedTopicFlow
    {
        private readonly IWorkflowActivityFactory _activities;

        public GeneratedStyleTopic(
            TopicWorkflowContext context,
            IWorkflowActivityFactory activities,
            Microsoft.Extensions.Logging.ILogger<GeneratedStyleTopic> logger)
            : base(context, logger, "generated")
        {
            _activities = activities;
        }

        protected override void ComposeWorkflow()
        {
            Add(_activities.CreatePrompt(new PromptActivityDefinition("welcome", "Welcome")));
            Add(_activities.CreateQuickAnswer(
                new QuickAnswerActivityDefinition("next", "Continue?", ["Yes", "No"])));
        }
    }
}
