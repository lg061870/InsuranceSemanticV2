using ConversaCore.BlazorTemplateHost.Configuration;
using ConversaCore.BlazorTemplateHost.Tools;
using ConversaCore.BlazorTemplateHost.Topics.SampleToolTopic;
using ConversaCore.Context;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.Tools;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Core;
using ConversaCore.UI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ConversaCore.Tests.Authoring;

public sealed class SampleToolGeneratedAuthoringTests
{
    [Fact]
    public async Task SampleToolTopic_InstantiatesThroughExplicitConstructor_AndComposesBoundedActivities()
    {
        var activations = new List<SampleToolTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var topic = ActivatorUtilities.CreateInstance<SampleToolTopic>(scope.ServiceProvider);

        Assert.NotNull(topic);
        await topic.InitializeAsync();
        Assert.Equal(ConversaCoreTopicRegistration.SampleToolTopicId, topic.Name);

        var activities = topic.GetAllActivities().ToList();
        Assert.Contains(activities, a => a.Id == SampleToolTopic.PromptActivityId);
        Assert.Contains(activities, a => a.Id == SampleToolTopic.LookupCardActivityId);
        Assert.Contains(activities, a => a.Id == SampleToolTopic.InvokeLookupActivityId);
        Assert.Contains(activities, a => a.Id == SampleToolTopic.ConfirmOrderActivityId);
        Assert.Contains(activities, a => a.Id == SampleToolTopic.BranchConfirmId);
        Assert.Contains(activities, a => a.Id == SampleToolTopic.BranchCancelId);
        Assert.Contains(activities, a => a.Id == SampleToolTopic.CompleteActivityId);

        Assert.Equal(1.0f, await topic.CanHandleAsync("sample tool"));
        Assert.Equal(1.0f, await topic.CanHandleAsync("lookup item"));
        Assert.Equal(1.0f, await topic.CanHandleAsync("order part"));
        Assert.Equal(0.0f, await topic.CanHandleAsync("unrelated topic"));
    }

    [Fact]
    public async Task SampleToolTopic_AffirmativePath_ExecutesLookupAndConfirmedOrderWithAllowlists()
    {
        var activations = new List<SampleToolTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var view = new ConversationOutputViewState();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = runtime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        await runtime.StartAsync(timeout.Token);

        // 1. Lookup card is presented
        var lookupCard = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, view,
            output => output.CardId == SampleToolTopic.LookupCardActivityId);
        Assert.Equal(SampleToolTopic.LookupCardActivityId, lookupCard.CardId);

        // 2. Submit lookup request for ITEM-101
        await runtime.SubmitCardAsync(new CardSubmission(lookupCard.CardId, new Dictionary<string, object>
        {
            [nameof(SampleLookupRequest.ItemId)] = "ITEM-101"
        }), timeout.Token);

        // 3. Confirm quick answer is presented after read-only tool runs
        var confirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, view,
            output => output.CardId == SampleToolTopic.ConfirmOrderActivityId);
        Assert.Equal(SampleToolTopic.ConfirmOrderActivityId, confirm.CardId);

        // 4. Affirmative human choice: User explicitly selects "Confirm Order"
        await runtime.SubmitCardAsync(new CardSubmission(confirm.CardId, new Dictionary<string, object>
        {
            ["answer"] = "Confirm Order"
        }), timeout.Token);

        // 5. Topic completes
        await ReadAndApplyUntilAsync<TopicLifecycleOutput>(outputs, view,
            output => output.TopicId == ConversaCoreTopicRegistration.SampleToolTopicId &&
                      output.State == ConversationTopicState.Completed);

        // 6. Assert read-only tool result in context
        var lookupResult = context.GetValue<ToolResult<SampleLookupResult>>(SampleToolTopic.LookupResultContextKey);
        Assert.NotNull(lookupResult);
        Assert.True(lookupResult!.Succeeded);
        Assert.Equal("ITEM-101", lookupResult.Value!.ItemId);
        Assert.Equal("Standard Widget", lookupResult.Value.Name);
        Assert.Equal(24.99m, lookupResult.Value.Price);

        // 7. Assert mutating tool result in context (placed with confirmation)
        var orderResult = context.GetValue<ToolResult<SampleOrderResult>>(SampleToolTopic.OrderResultContextKey);
        Assert.NotNull(orderResult);
        Assert.True(orderResult!.Succeeded);
        Assert.Equal("ITEM-101", orderResult.Value!.ItemId);
        Assert.Equal("Confirmed", orderResult.Value.Status);
        Assert.StartsWith("ORD-", orderResult.Value.OrderId, StringComparison.Ordinal);

        // 8. Verify UI projection messages
        Assert.Contains(view.Messages, item => item.Content.Contains(orderResult.Value.OrderId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SampleToolTopic_CancelPath_BypassesMutatingTool()
    {
        var activations = new List<SampleToolTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var view = new ConversationOutputViewState();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = runtime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        await runtime.StartAsync(timeout.Token);

        var lookupCard = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, view,
            output => output.CardId == SampleToolTopic.LookupCardActivityId);

        await runtime.SubmitCardAsync(new CardSubmission(lookupCard.CardId, new Dictionary<string, object>
        {
            [nameof(SampleLookupRequest.ItemId)] = "ITEM-102"
        }), timeout.Token);

        var confirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, view,
            output => output.CardId == SampleToolTopic.ConfirmOrderActivityId);

        // User cancels order
        await runtime.SubmitCardAsync(new CardSubmission(confirm.CardId, new Dictionary<string, object>
        {
            ["answer"] = "Cancel"
        }), timeout.Token);

        await ReadAndApplyUntilAsync<TopicLifecycleOutput>(outputs, view,
            output => output.TopicId == ConversaCoreTopicRegistration.SampleToolTopicId &&
                      output.State == ConversationTopicState.Completed);

        // Read-only lookup executed
        var lookupResult = context.GetValue<ToolResult<SampleLookupResult>>(SampleToolTopic.LookupResultContextKey);
        Assert.NotNull(lookupResult);
        Assert.Equal("ITEM-102", lookupResult!.Value!.ItemId);

        // Mutating tool was NOT executed
        var orderResult = context.GetValue<ToolResult<SampleOrderResult>>(SampleToolTopic.OrderResultContextKey);
        Assert.Null(orderResult);

        // Fallback cancellation message emitted to UI
        Assert.Contains(view.Messages, item => item.Content.Contains("Order cancelled. No modifications were made", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SampleToolTopic_TopicAllowlist_RejectsUndeclaredTool()
    {
        var activations = new List<SampleToolTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IToolExecutor>();

        // Execution context declares ONLY sample.item.lookup in its topic allowlist
        var context = new ToolExecutionContext
        {
            ConversationId = Guid.NewGuid().ToString("N"),
            Subject = "user",
            CorrelationId = Guid.NewGuid().ToString("N"),
            Services = scope.ServiceProvider,
            AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                SampleLookupTool.ToolId
            },
            ConfirmationGranted = true,
            TrustedIdentityValidated = true,
            IdempotencyKey = "idem-key"
        };

        // Attempting to execute sample.order.create which is not in AllowedToolIds
        var result = await executor.ExecuteAsync<SampleOrderRequest, SampleOrderResult>(
            SampleOrderTool.ToolId,
            new SampleOrderRequest { ItemId = "ITEM-101", Quantity = 1, CustomerName = "Test" },
            context);

        Assert.False(result.Succeeded);
        Assert.Equal("tool_not_allowed", result.ErrorCode);
        Assert.Contains("The current topic has not declared this tool", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SampleToolTopic_MutatingTool_RequiresConfirmation_RejectsUnconfirmed()
    {
        var activations = new List<SampleToolTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IToolExecutor>();

        // Execution context has ConfirmationGranted = false
        var context = new ToolExecutionContext
        {
            ConversationId = Guid.NewGuid().ToString("N"),
            Subject = "user",
            CorrelationId = Guid.NewGuid().ToString("N"),
            Services = scope.ServiceProvider,
            AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                SampleOrderTool.ToolId
            },
            ConfirmationGranted = false, // Unconfirmed!
            TrustedIdentityValidated = true,
            IdempotencyKey = "idem-key"
        };

        var result = await executor.ExecuteAsync<SampleOrderRequest, SampleOrderResult>(
            SampleOrderTool.ToolId,
            new SampleOrderRequest { ItemId = "ITEM-101", Quantity = 1, CustomerName = "Test" },
            context);

        Assert.False(result.Succeeded);
        Assert.Equal("confirmation_required", result.ErrorCode);
        Assert.Contains("Explicit confirmation is required", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SampleToolTopic_MutatingTool_RequiresTrustedIdentity()
    {
        var activations = new List<SampleToolTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<IToolExecutor>();

        // Execution context has ConfirmationGranted = true but TrustedIdentityValidated = false
        var context = new ToolExecutionContext
        {
            ConversationId = Guid.NewGuid().ToString("N"),
            Subject = "user",
            CorrelationId = Guid.NewGuid().ToString("N"),
            Services = scope.ServiceProvider,
            AllowedToolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                SampleOrderTool.ToolId
            },
            ConfirmationGranted = true,
            TrustedIdentityValidated = false, // Not validated!
            IdempotencyKey = "idem-key"
        };

        var result = await executor.ExecuteAsync<SampleOrderRequest, SampleOrderResult>(
            SampleOrderTool.ToolId,
            new SampleOrderRequest { ItemId = "ITEM-101", Quantity = 1, CustomerName = "Test" },
            context);

        Assert.False(result.Succeeded);
        Assert.Equal("identity_not_validated", result.ErrorCode);
        Assert.Contains("identity was not validated", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SampleToolTopic_Reset_ClearsContext_TerminatesTopic_AndRecomposesCleanly()
    {
        var activations = new List<SampleToolTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = runtime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        await runtime.StartAsync(timeout.Token);
        var lookupCard = await ReadUntilAsync<AdaptiveCardOutput>(outputs,
            output => output.CardId == SampleToolTopic.LookupCardActivityId);

        await runtime.SubmitCardAsync(new CardSubmission(lookupCard.CardId, new Dictionary<string, object>
        {
            [nameof(SampleLookupRequest.ItemId)] = "ITEM-101"
        }), timeout.Token);

        var confirm = await ReadUntilAsync<AdaptiveCardOutput>(outputs,
            output => output.CardId == SampleToolTopic.ConfirmOrderActivityId);

        await runtime.SubmitCardAsync(new CardSubmission(confirm.CardId, new Dictionary<string, object>
        {
            ["answer"] = "Confirm Order"
        }), timeout.Token);

        await ReadUntilAsync<TopicLifecycleOutput>(outputs,
            output => output.State == ConversationTopicState.Completed);
        Assert.NotNull(context.GetValue<ToolResult<SampleOrderResult>>(SampleToolTopic.OrderResultContextKey));

        // Reset runtime
        await runtime.ResetAsync(timeout.Token);
        var restartedLookup = await ReadUntilAsync<AdaptiveCardOutput>(outputs,
            output => output.CardId == SampleToolTopic.LookupCardActivityId);

        Assert.Equal(SampleToolTopic.LookupCardActivityId, restartedLookup.CardId);
        Assert.Equal(2, activations.Count);
        Assert.True(activations[0].IsTerminated);
        Assert.False(activations[1].IsTerminated);
        Assert.Null(context.GetValue<ToolResult<SampleOrderResult>>(SampleToolTopic.OrderResultContextKey));
        Assert.NotSame(
            activations[0].GetAllActivities().FirstOrDefault(),
            activations[1].GetAllActivities().First());
    }

    private static ServiceProvider BuildProvider(List<SampleToolTopic> activations)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton<IChatCompletionService>(new FakeChatCompletionService());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(kernelBuilder.Build());
        services.AddScoped<IConversationContext>(_ => new ConversationContext(
            Guid.NewGuid().ToString("N"), "subject", NullLogger<ConversationContext>.Instance));

        new ConversaCoreBuilder(services)
            .AddTopic<SampleToolTopic>(ConversaCoreTopicRegistration.SampleToolTopicId, serviceProvider =>
            {
                var topic = ActivatorUtilities.CreateInstance<SampleToolTopic>(serviceProvider);
                lock (activations) activations.Add(topic);
                return topic;
            })
            .AddConversaCoreTools()
            .AddConversationRuntime(ConversaCoreTopicRegistration.SampleToolTopicId);

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
                [new ChatMessageContent(AuthorRole.Assistant, "Welcome to the ConversaCore tool sample topic.")]);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
