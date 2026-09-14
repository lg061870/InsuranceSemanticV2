using ConversaCore.Context;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.UI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ConversaCore.Tests.Authoring;

public sealed class GeneratedAuthoringRuntimeUiContractTests
{
    [Fact]
    public async Task GeneratedTopic_ActivatesAndCompletesThroughRuntimeAndUiProjection()
    {
        var activations = new List<GeneratedStyleContractTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var view = new ConversationOutputViewState();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = runtime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        await runtime.StartAsync(timeout.Token);
        var confirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, view,
            output => output.CardId == "generated.confirm");
        Assert.Equal("generated.confirm", confirm.CardId);
        await ReadAndApplyUntilAsync<PromptStateOutput>(outputs, view,
            output => output.State == ConversationPromptState.Disabled);
        Assert.False(view.IsPromptEnabled);

        await runtime.SubmitCardAsync(new CardSubmission(
            confirm.CardId, new Dictionary<string, object> { ["answer"] = "Yes" }), timeout.Token);
        var profile = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, view,
            output => output.CardId == "generated.profile");
        Assert.Contains("Generated profile", profile.CardJson, StringComparison.Ordinal);

        await runtime.SubmitCardAsync(new CardSubmission(profile.CardId, new Dictionary<string, object>
        {
            ["FullName"] = "Ada Lovelace",
            ["Plan"] = "plus"
        }), timeout.Token);
        await ReadAndApplyUntilAsync<TopicLifecycleOutput>(outputs, view,
            output => output.TopicId == "generated.start" &&
                      output.State == ConversationTopicState.Completed);

        var model = context.GetValue<GeneratedProfileModel>("generated.profile.model");
        Assert.NotNull(model);
        Assert.Equal("Ada Lovelace", model!.FullName);
        Assert.Equal("plus", model.Plan);
        Assert.Single(activations);
        Assert.Contains(view.Messages, item => item.IsAdaptiveCard && item.CardId == "generated.confirm");
        Assert.Contains(view.Messages, item => item.IsAdaptiveCard && item.CardId == "generated.profile");
        Assert.Contains(view.Messages, item => item.Content == "Generated workflow complete.");
    }

    [Fact]
    public async Task Reset_TerminatesOldGeneratedTopic_ClearsState_AndRecomposesFreshGraph()
    {
        var activations = new List<GeneratedStyleContractTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = runtime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        await runtime.StartAsync(timeout.Token);
        var confirm = await ReadUntilAsync<AdaptiveCardOutput>(outputs,
            output => output.CardId == "generated.confirm");
        await runtime.SubmitCardAsync(new CardSubmission(
            confirm.CardId, new Dictionary<string, object> { ["answer"] = "Yes" }), timeout.Token);
        var profile = await ReadUntilAsync<AdaptiveCardOutput>(outputs,
            output => output.CardId == "generated.profile");
        await runtime.SubmitCardAsync(new CardSubmission(profile.CardId, new Dictionary<string, object>
        {
            ["FullName"] = "Before Reset",
            ["Plan"] = "basic"
        }), timeout.Token);
        await ReadUntilAsync<TopicLifecycleOutput>(outputs,
            output => output.State == ConversationTopicState.Completed);
        Assert.NotNull(context.GetValue<GeneratedProfileModel>("generated.profile.model"));

        await runtime.ResetAsync(timeout.Token);
        var restarted = await ReadUntilAsync<AdaptiveCardOutput>(outputs,
            output => output.CardId == "generated.confirm");

        Assert.Equal("generated.confirm", restarted.CardId);
        Assert.Equal(2, activations.Count);
        Assert.True(activations[0].IsTerminated);
        Assert.False(activations[1].IsTerminated);
        Assert.Null(context.GetValue<GeneratedProfileModel>("generated.profile.model"));
        Assert.NotSame(
            activations[0].GetAllActivities().FirstOrDefault(),
            activations[1].GetAllActivities().First());
    }

    [Fact]
    public async Task ConcurrentScopes_IsolateGeneratedStateOutputsAndUiProjection()
    {
        var activations = new List<GeneratedStyleContractTopic>();
        await using var provider = BuildProvider(activations);
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstRuntime = firstScope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var secondRuntime = secondScope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var firstView = new ConversationOutputViewState();
        var secondView = new ConversationOutputViewState();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var firstOutputs = firstRuntime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();
        await using var secondOutputs = secondRuntime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        await Task.WhenAll(firstRuntime.StartAsync(timeout.Token), secondRuntime.StartAsync(timeout.Token));
        var firstConfirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(firstOutputs, firstView,
            output => output.CardId == "generated.confirm");
        var secondConfirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(secondOutputs, secondView,
            output => output.CardId == "generated.confirm");
        await Task.WhenAll(
            firstRuntime.SubmitCardAsync(new CardSubmission(firstConfirm.CardId,
                new Dictionary<string, object> { ["answer"] = "Yes" }), timeout.Token),
            secondRuntime.SubmitCardAsync(new CardSubmission(secondConfirm.CardId,
                new Dictionary<string, object> { ["answer"] = "Yes" }), timeout.Token));
        var firstProfile = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(firstOutputs, firstView,
            output => output.CardId == "generated.profile");
        var secondProfile = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(secondOutputs, secondView,
            output => output.CardId == "generated.profile");
        await Task.WhenAll(
            firstRuntime.SubmitCardAsync(new CardSubmission(firstProfile.CardId,
                new Dictionary<string, object> { ["FullName"] = "First", ["Plan"] = "basic" }), timeout.Token),
            secondRuntime.SubmitCardAsync(new CardSubmission(secondProfile.CardId,
                new Dictionary<string, object> { ["FullName"] = "Second", ["Plan"] = "plus" }), timeout.Token));
        await ReadAndApplyUntilAsync<TopicLifecycleOutput>(firstOutputs, firstView,
            output => output.State == ConversationTopicState.Completed);
        await ReadAndApplyUntilAsync<TopicLifecycleOutput>(secondOutputs, secondView,
            output => output.State == ConversationTopicState.Completed);

        Assert.NotEqual(firstRuntime.ConversationId, secondRuntime.ConversationId);
        Assert.Equal("First", firstContext.GetValue<GeneratedProfileModel>("generated.profile.model")!.FullName);
        Assert.Equal("Second", secondContext.GetValue<GeneratedProfileModel>("generated.profile.model")!.FullName);
        Assert.Equal(2, activations.Count);
        Assert.NotSame(activations[0], activations[1]);
        Assert.Equal(2, firstView.Messages.Count(item => item.IsAdaptiveCard));
        Assert.Equal(2, secondView.Messages.Count(item => item.IsAdaptiveCard));
    }

    [Fact]
    public async Task ScopeDisposal_TerminatesGeneratedTopicAndCompletesOutputs()
    {
        var activations = new List<GeneratedStyleContractTopic>();
        await using var provider = BuildProvider(activations);
        var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var subscription = runtime.Subscribe();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = subscription.ReadAllAsync(timeout.Token).GetAsyncEnumerator();
        await runtime.StartAsync(timeout.Token);
        await ReadUntilAsync<AdaptiveCardOutput>(outputs,
            output => output.CardId == "generated.confirm");

        await scope.DisposeAsync();

        Assert.True(Assert.Single(activations).IsTerminated);
        var bufferedOutputCount = 0;
        while (await outputs.MoveNextAsync())
            Assert.True(++bufferedOutputCount < 20, "Disposed output stream did not complete.");
        Assert.Throws<ObjectDisposedException>(() => runtime.Subscribe());
    }

    private static ServiceProvider BuildProvider(List<GeneratedStyleContractTopic> activations)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton<IChatCompletionService>(new FakeChatCompletionService());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(kernelBuilder.Build());
        services.AddScoped<IConversationContext>(_ => new ConversationContext(
            Guid.NewGuid().ToString("N"), "subject", NullLogger<ConversationContext>.Instance));
        new ConversaCoreBuilder(services)
            .AddTopic<GeneratedStyleContractTopic>("generated.start", serviceProvider =>
            {
                var topic = ActivatorUtilities.CreateInstance<GeneratedStyleContractTopic>(serviceProvider);
                lock (activations) activations.Add(topic);
                return topic;
            })
            .AddConversationRuntime("generated.start");
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
                [new ChatMessageContent(AuthorRole.Assistant, "Generated welcome")]);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
