using ConversaCore.BlazorTemplateHost.Configuration;
using ConversaCore.BlazorTemplateHost.Contracts;
using ConversaCore.BlazorTemplateHost.Topics.SampleHostOutputTopic;
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

public sealed class SampleHostOutputGeneratedAuthoringTests
{
    [Fact]
    public async Task SampleHostOutputTopic_InstantiatesThroughExplicitConstructor_AndComposesBoundedActivities()
    {
        var activations = new List<SampleHostOutputTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var topic = ActivatorUtilities.CreateInstance<SampleHostOutputTopic>(scope.ServiceProvider);

        Assert.NotNull(topic);
        await topic.InitializeAsync();
        Assert.Equal(ConversaCoreTopicRegistration.SampleHostOutputTopicId, topic.Name);

        var activities = topic.GetAllActivities().ToList();
        Assert.Contains(activities, a => a.Id == SampleHostOutputTopic.PromptWelcomeActivityId);
        Assert.Contains(activities, a => a.Id == SampleHostOutputTopic.NotifyStartedActivityId);
        Assert.Contains(activities, a => a.Id == SampleHostOutputTopic.PromptInteractionActivityId);
        Assert.Contains(activities, a => a.Id == SampleHostOutputTopic.InvokeInteractionActivityId);
        Assert.Contains(activities, a => a.Id == SampleHostOutputTopic.CompleteActivityId);
        Assert.Contains(activities, a => a.Id == SampleHostOutputTopic.NotifyCompletedActivityId);

        Assert.Equal(1.0f, await topic.CanHandleAsync("sample host"));
        Assert.Equal(1.0f, await topic.CanHandleAsync("host notification"));
        Assert.Equal(1.0f, await topic.CanHandleAsync("interaction decision"));
        Assert.Equal(0.0f, await topic.CanHandleAsync("unrelated topic"));
    }

    [Fact]
    public async Task SampleHostOutputTopic_FullFlow_DispatchesNotification_AwaitsInteraction_AndCompletes()
    {
        var activations = new List<SampleHostOutputTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var session = scope.ServiceProvider.GetRequiredService<IConversationSession>();
        var view = new ConversationOutputViewState();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = runtime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        var startTask = runtime.StartAsync(timeout.Token);

        // 1. One-way host notification is emitted
        var initialNotification = await ReadAndApplyUntilAsync<HostNotification<SampleHostNotification>>(
            outputs, view, n => n.Payload.NotificationId == "NOTIF-001");
        Assert.Equal("sample.host.notification", initialNotification.EventName);
        Assert.Equal(1, initialNotification.Version);
        Assert.Equal("Host Topic Initiated", initialNotification.Payload.Title);
        Assert.Equal("Info", initialNotification.Payload.Severity);

        // 2. Correlated host interaction request is emitted and pending
        var interactionRequest = await ReadAndApplyUntilAsync<HostInteractionRequest<SampleHostInteractionRequest, SampleHostInteractionResponse>>(
            outputs, view, r => r.InteractionName == "sample.host.decision");
        Assert.Equal("sample.host.decision", interactionRequest.InteractionName);
        Assert.Equal(1, interactionRequest.Version);
        Assert.Equal("REQ-101", interactionRequest.Request.RequestId);
        Assert.True(session.IsHostInteractionPending(interactionRequest.RequestId));

        // 3. Host responds through typed ConversationHostOutputContext
        var hostOutputContext = new ConversationHostOutputContext(interactionRequest, runtime);
        Assert.True(hostOutputContext.RequiresResponse);
        await hostOutputContext.RespondAsync(new SampleHostInteractionResponse(
            SelectedOption: "Priority Tier",
            Comments: "Approved by manager via host interface",
            Confirmed: true), timeout.Token);

        // Await start task completion now that host response was delivered
        await startTask;

        // 4. Final completion notification is emitted
        var completionNotification = await ReadAndApplyUntilAsync<HostNotification<SampleHostNotification>>(
            outputs, view, n => n.Payload.NotificationId == "NOTIF-002");
        Assert.Equal("NOTIF-002", completionNotification.Payload.NotificationId);
        Assert.Equal("Success", completionNotification.Payload.Severity);
        Assert.Contains("Priority Tier", completionNotification.Payload.Message, StringComparison.Ordinal);

        // 5. Topic completes
        await ReadAndApplyUntilAsync<TopicLifecycleOutput>(outputs, view,
            output => output.TopicId == ConversaCoreTopicRegistration.SampleHostOutputTopicId &&
                      output.State == ConversationTopicState.Completed);

        // 6. Assert workflow context holds the typed response
        var storedResponse = context.GetValue<SampleHostInteractionResponse>(SampleHostOutputTopic.InteractionResultContextKey);
        Assert.NotNull(storedResponse);
        Assert.Equal("Priority Tier", storedResponse.SelectedOption);
        Assert.True(storedResponse.Confirmed);

        // 7. Assert pending interaction is fully resolved
        Assert.Empty(session.PendingHostInteractionIds);

        // 8. Assert host outputs did NOT leak into the standard text transcript
        Assert.DoesNotContain(view.Messages, m => m.Content.Contains("NOTIF-001", StringComparison.Ordinal));
        Assert.DoesNotContain(view.Messages, m => m.Content.Contains("REQ-101", StringComparison.Ordinal));
        // But the completion summary message emitted to standard chat transcript DOES mention the choice
        Assert.Contains(view.Messages, m => m.Content.Contains("Priority Tier", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SampleHostOutputTopic_Reset_ClearsPendingState_AndRecomposesCleanly()
    {
        var activations = new List<SampleHostOutputTopic>();
        await using var provider = BuildProvider(activations);
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var session = scope.ServiceProvider.GetRequiredService<IConversationSession>();
        var view = new ConversationOutputViewState();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var outputs = runtime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        var startTask = runtime.StartAsync(timeout.Token);

        // 1. Wait until first host interaction is pending
        var interactionRequest = await ReadAndApplyUntilAsync<HostInteractionRequest<SampleHostInteractionRequest, SampleHostInteractionResponse>>(
            outputs, view, r => r.InteractionName == "sample.host.decision");
        Assert.True(session.IsHostInteractionPending(interactionRequest.RequestId));

        // 2. Reset runtime: previous run is cancelled
        var resetTask = runtime.ResetAsync(timeout.Token);
        await Assert.ThrowsAsync<OperationCanceledException>(() => startTask);

        // 3. Restarted topic dispatches fresh interaction request
        var restartedRequest = await ReadAndApplyUntilAsync<HostInteractionRequest<SampleHostInteractionRequest, SampleHostInteractionResponse>>(
            outputs, view, r => r.InteractionName == "sample.host.decision");
        Assert.True(session.IsHostInteractionPending(restartedRequest.RequestId));

        // Old activation is terminated, new activation is active
        Assert.Equal(2, activations.Count);
        Assert.True(activations[0].IsTerminated);
        Assert.False(activations[1].IsTerminated);

        // 4. Host responds to restarted interaction
        var hostContext2 = new ConversationHostOutputContext(restartedRequest, runtime);
        await hostContext2.RespondAsync(new SampleHostInteractionResponse(
            SelectedOption: "Standard Tier",
            Comments: "Approved on reset",
            Confirmed: true), timeout.Token);

        // 5. Reset completes cleanly
        await resetTask;
        Assert.True(activations[1].IsTerminated);
    }

    private static ServiceProvider BuildProvider(List<SampleHostOutputTopic> activations)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton<IChatCompletionService>(new FakeChatCompletionService());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(kernelBuilder.Build());
        services.AddScoped<IConversationContext>(_ => new ConversationContext(
            Guid.NewGuid().ToString("N"), "subject", NullLogger<ConversationContext>.Instance));

        new ConversaCoreBuilder(services)
            .AddTopic<SampleHostOutputTopic>(ConversaCoreTopicRegistration.SampleHostOutputTopicId, serviceProvider =>
            {
                var topic = ActivatorUtilities.CreateInstance<SampleHostOutputTopic>(serviceProvider);
                lock (activations) activations.Add(topic);
                return topic;
            })
            .AddConversationRuntime(ConversaCoreTopicRegistration.SampleHostOutputTopicId);

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
                [new ChatMessageContent(AuthorRole.Assistant, "Demonstrating host output capabilities.")]);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
