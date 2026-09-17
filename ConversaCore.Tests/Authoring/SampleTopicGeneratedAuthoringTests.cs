using ConversaCore.BlazorTemplateHost.Configuration;
using ConversaCore.BlazorTemplateHost.Topics.SampleTopic;
using ConversaCore.BlazorTemplateHost.Topics.SampleTopic.Models;
using ConversaCore.Context;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Core;
using ConversaCore.UI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ConversaCore.Tests.Authoring;

public sealed class SampleTopicGeneratedAuthoringTests
{
    [Fact]
    public async Task SampleTopic_InstantiatesThroughExplicitConstructor_AndComposesBoundedActivities()
    {
        var activations = new List<SampleTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var topic = ActivatorUtilities.CreateInstance<SampleTopic>(scope.ServiceProvider);

        Assert.NotNull(topic);
        await topic.InitializeAsync();
        Assert.Equal(ConversaCoreTopicRegistration.SampleTopicId, topic.Name);

        var activities = topic.GetAllActivities().ToList();
        Assert.Contains(activities, a => a.Id == SampleTopic.PromptActivityId);
        Assert.Contains(activities, a => a.Id == SampleTopic.ConfirmActivityId);
        Assert.Contains(activities, a => a.Id == "sample.branch-yes");
        Assert.Contains(activities, a => a.Id == "sample.branch-fallback");
        Assert.Contains(activities, a => a.Id == SampleTopic.CompleteActivityId);

        Assert.Equal(1.0f, await topic.CanHandleAsync(string.Empty));
        Assert.Equal(1.0f, await topic.CanHandleAsync("sample topic"));
        Assert.Equal(1.0f, await topic.CanHandleAsync("Can I see the sample?"));
        Assert.Equal(0.0f, await topic.CanHandleAsync("insurance quote"));
    }

    [Fact]
    public async Task SampleTopic_AffirmativePath_CollectsInputAndCompletesWithTypedModel()
    {
        var activations = new List<SampleTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var view = new ConversationOutputViewState();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = runtime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        await runtime.StartAsync(timeout.Token);

        // 1. Confirm quick answer is presented
        var confirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, view,
            output => output.CardId == SampleTopic.ConfirmActivityId);
        Assert.Equal(SampleTopic.ConfirmActivityId, confirm.CardId);

        // 2. Affirmative choice: user selects "Yes"
        await runtime.SubmitCardAsync(new CardSubmission(
            confirm.CardId, new Dictionary<string, object> { ["answer"] = "Yes" }), timeout.Token);

        // 3. Adaptive card for typed input model is presented
        var inputCard = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, view,
            output => output.CardId == SampleTopic.InputCardActivityId);
        Assert.Equal(SampleTopic.InputCardActivityId, inputCard.CardId);
        Assert.Contains("Explore Sample Topic", inputCard.CardJson, StringComparison.Ordinal);

        // 4. Submit typed model input
        await runtime.SubmitCardAsync(new CardSubmission(inputCard.CardId, new Dictionary<string, object>
        {
            [nameof(SampleInputModel.Question)] = "How do I use generated topics in ConversaCore?"
        }), timeout.Token);

        // 5. Verify completion lifecycle
        await ReadAndApplyUntilAsync<TopicLifecycleOutput>(outputs, view,
            output => output.TopicId == ConversaCoreTopicRegistration.SampleTopicId &&
                      output.State == ConversationTopicState.Completed);

        // 6. Assert typed model is persisted in context
        var model = context.GetValue<SampleInputModel>(SampleTopic.InputModelContextKey);
        Assert.NotNull(model);
        Assert.Equal("How do I use generated topics in ConversaCore?", model!.Question);
        Assert.Single(activations);

        // 7. Verify UI projection messages
        Assert.Contains(view.Messages, item => item.IsAdaptiveCard && item.CardId == SampleTopic.ConfirmActivityId);
        Assert.Contains(view.Messages, item => item.IsAdaptiveCard && item.CardId == SampleTopic.InputCardActivityId);
        Assert.Contains(view.Messages, item => item.Content.Contains("How do I use generated topics in ConversaCore?", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SampleTopic_DeclinePath_RoutesToFallbackAndCompletesWithoutModel()
    {
        var activations = new List<SampleTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var view = new ConversationOutputViewState();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = runtime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        await runtime.StartAsync(timeout.Token);

        var confirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, view,
            output => output.CardId == SampleTopic.ConfirmActivityId);

        // User declines to explore
        await runtime.SubmitCardAsync(new CardSubmission(
            confirm.CardId, new Dictionary<string, object> { ["answer"] = "No" }), timeout.Token);

        // Verify completion without input card prompt
        await ReadAndApplyUntilAsync<TopicLifecycleOutput>(outputs, view,
            output => output.TopicId == ConversaCoreTopicRegistration.SampleTopicId &&
                      output.State == ConversationTopicState.Completed);

        // Context must not contain input model
        var model = context.GetValue<SampleInputModel>(SampleTopic.InputModelContextKey);
        Assert.Null(model);

        // UI projection includes fallback message
        Assert.Contains(view.Messages, item => item.Content.Contains("No problem! You can explore the sample topic whenever you are ready.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SampleTopic_Reset_ClearsContext_TerminatesTopic_AndRecomposesCleanly()
    {
        var activations = new List<SampleTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = runtime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        await runtime.StartAsync(timeout.Token);
        var confirm = await ReadUntilAsync<AdaptiveCardOutput>(outputs,
            output => output.CardId == SampleTopic.ConfirmActivityId);

        await runtime.SubmitCardAsync(new CardSubmission(
            confirm.CardId, new Dictionary<string, object> { ["answer"] = "Yes" }), timeout.Token);
        var inputCard = await ReadUntilAsync<AdaptiveCardOutput>(outputs,
            output => output.CardId == SampleTopic.InputCardActivityId);

        await runtime.SubmitCardAsync(new CardSubmission(inputCard.CardId, new Dictionary<string, object>
        {
            [nameof(SampleInputModel.Question)] = "First attempt"
        }), timeout.Token);

        await ReadUntilAsync<TopicLifecycleOutput>(outputs,
            output => output.State == ConversationTopicState.Completed);
        Assert.NotNull(context.GetValue<SampleInputModel>(SampleTopic.InputModelContextKey));

        // Reset runtime
        await runtime.ResetAsync(timeout.Token);
        var restartedConfirm = await ReadUntilAsync<AdaptiveCardOutput>(outputs,
            output => output.CardId == SampleTopic.ConfirmActivityId);

        Assert.Equal(SampleTopic.ConfirmActivityId, restartedConfirm.CardId);
        Assert.Equal(2, activations.Count);
        Assert.True(activations[0].IsTerminated);
        Assert.False(activations[1].IsTerminated);
        Assert.Null(context.GetValue<SampleInputModel>(SampleTopic.InputModelContextKey));
        Assert.NotSame(
            activations[0].GetAllActivities().FirstOrDefault(),
            activations[1].GetAllActivities().First());
    }

    private static ServiceProvider BuildProvider(List<SampleTopic> activations)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton<IChatCompletionService>(new FakeChatCompletionService());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(kernelBuilder.Build());
        services.AddScoped<IConversationContext>(_ => new ConversationContext(
            Guid.NewGuid().ToString("N"), "subject", NullLogger<ConversationContext>.Instance));

        new ConversaCoreBuilder(services)
            .AddTopic<SampleTopic>(ConversaCoreTopicRegistration.SampleTopicId, serviceProvider =>
            {
                var topic = ActivatorUtilities.CreateInstance<SampleTopic>(serviceProvider);
                lock (activations) activations.Add(topic);
                return topic;
            })
            .AddConversationRuntime(ConversaCoreTopicRegistration.SampleTopicId);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private static async Task<TOutput> ReadAndApplyUntilAsync<TOutput>(
        IAsyncEnumerator<ConversationOutput> outputs,
        ConversationOutputViewState view,
        Func<TOutput, bool>? predicate = null)
        where TOutput : ConversationOutput
    {
        while (await outputs.MoveNextAsync())
        {
            view.Apply(outputs.Current);
            if (outputs.Current is TOutput match && (predicate is null || predicate(match)))
                return match;
        }

        throw new InvalidOperationException($"Output {typeof(TOutput).Name} was not dispatched.");
    }

    private static async Task<TOutput> ReadUntilAsync<TOutput>(
        IAsyncEnumerator<ConversationOutput> outputs,
        Func<TOutput, bool>? predicate = null)
        where TOutput : ConversationOutput
    {
        while (await outputs.MoveNextAsync())
        {
            if (outputs.Current is TOutput match && (predicate is null || predicate(match)))
                return match;
        }

        throw new InvalidOperationException($"Output {typeof(TOutput).Name} was not dispatched.");
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
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<ChatMessageContent>>(
                [new ChatMessageContent(AuthorRole.Assistant, "Welcome to the ConversaCore sample topic.")]);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
