using System.ComponentModel.DataAnnotations;
using ConversaCore.Events;
using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConversaCore.Tests.Runtime;

public sealed class ConversationRuntimeTests
{
    [Fact]
    public async Task Registration_ResolvesOneRuntimePerScopeWithoutEagerTopicActivation()
    {
        var activations = 0;
        var services = Services();
        new ConversaCoreBuilder(services)
            .AddTopic<ITopic>("start", _ =>
            {
                activations++;
                return new ScriptedTopic("start", Waiting());
            })
            .AddConversationRuntime("start");

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Assert.Equal(0, activations);
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var firstRuntime = first.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var sameRuntime = first.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var secondRuntime = second.ServiceProvider.GetRequiredService<IConversationRuntime>();

        Assert.Same(firstRuntime, sameRuntime);
        Assert.NotSame(firstRuntime, secondRuntime);
        Assert.NotEqual(firstRuntime.ConversationId, secondRuntime.ConversationId);
        Assert.Equal(0, activations);
    }

    [Fact]
    public async Task Resolution_RejectsAnUnregisteredStartTopic()
    {
        var services = Services();
        new ConversaCoreBuilder(services)
            .AddTopic<ITopic>("other", _ => new ScriptedTopic("other", Waiting()))
            .AddConversationRuntime("missing");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IConversationRuntime>());

        Assert.Contains("Conversation start topic 'missing' is not registered", exception.Message);
    }

    [Fact]
    public async Task StartIsIdempotent_AndMessageUsesTheRetainedStartActivation()
    {
        ScriptedTopic? activated = null;
        var activationCount = 0;
        var services = Services();
        new ConversaCoreBuilder(services)
            .AddTopic<ITopic>("start", _ =>
            {
                activationCount++;
                return activated = new ScriptedTopic("start", Waiting(), Completed());
            })
            .AddConversationRuntime("start");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();

        await Task.WhenAll(runtime.StartAsync(), runtime.StartAsync());
        await runtime.SendMessageAsync("continue");

        Assert.Equal(1, activationCount);
        Assert.Equal([string.Empty, "continue"], activated!.Messages);
    }

    [Fact]
    public async Task CardSubmission_ValidatesActiveCardAndAdvancesARealLegacyFlow()
    {
        CardFlow? flow = null;
        var services = Services();
        new ConversaCoreBuilder(services)
            .AddTopic<CardFlow>("start", sp => flow = new CardFlow(
                sp.GetRequiredService<ILogger<CardFlow>>()))
            .AddConversationRuntime("start");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var subscription = runtime.Subscribe();
        await using var outputs = subscription.ReadAllAsync().GetAsyncEnumerator();

        await runtime.StartAsync();
        var card = await ReadUntilAsync<AdaptiveCardOutput>(outputs);
        Assert.Equal("appointment-card", card.CardId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.SubmitCardAsync(
            new CardSubmission("stale-card", new Dictionary<string, object> { ["Name"] = "Ada" })));

        await runtime.SubmitCardAsync(new CardSubmission(
            "appointment-card", new Dictionary<string, object> { ["Name"] = "Ada" }));
        var completed = await ReadUntilAsync<TopicLifecycleOutput>(
            outputs,
            output => output.State == ConversationTopicState.Completed);

        Assert.Equal("start", completed.TopicId);
        Assert.True(flow!.IsTerminated);
    }

    [Fact]
    public async Task ResetDisposesOldActivationAndRestartsWithFreshState()
    {
        var activations = new List<ScriptedTopic>();
        var services = Services();
        new ConversaCoreBuilder(services)
            .AddTopic<ITopic>("start", _ =>
            {
                var topic = new ScriptedTopic("start", Waiting());
                activations.Add(topic);
                return topic;
            })
            .AddConversationRuntime("start");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var session = scope.ServiceProvider.GetRequiredService<IConversationSession>();

        await runtime.StartAsync();
        session.SetValue("transient", "value");
        await runtime.ResetAsync();

        Assert.Equal(2, activations.Count);
        Assert.True(activations[0].Disposed);
        Assert.False(activations[1].Disposed);
        Assert.False(session.HasValue("transient"));
        Assert.Equal(["start"], session.TopicHistory);
    }

    [Fact]
    public async Task ScopeDisposal_CancelsPendingInteractionAndCompletesOutputSubscription()
    {
        var services = Services();
        new ConversaCoreBuilder(services)
            .AddTopic<ITopic>("start", _ => new ScriptedTopic("start", Waiting()))
            .AddConversationRuntime("start");
        await using var provider = services.BuildServiceProvider();
        var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var coordinator = scope.ServiceProvider.GetRequiredService<IHostInteractionCoordinator>();
        var subscription = runtime.Subscribe();
        await using var outputs = subscription.ReadAllAsync().GetAsyncEnumerator();
        await runtime.StartAsync();
        var pending = coordinator.RequestAsync<string, bool>(
            "site.confirm", 1, "Continue?", TimeSpan.FromSeconds(10));
        await ReadUntilAsync<HostInteractionRequestOutput>(outputs);

        await scope.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        Assert.False(await outputs.MoveNextAsync());
        Assert.Throws<ObjectDisposedException>(() => runtime.Subscribe());
    }

    [Fact]
    public async Task Reset_CancelsPendingLegacyInteractionDisposesItsLeaseAndRestarts()
    {
        var activationCount = 0;
        EventFlow? firstFlow = null;
        var services = Services();
        new ConversaCoreBuilder(services)
            .AddTopic<ITopic>("start", _ =>
            {
                activationCount++;
                if (activationCount == 1)
                    return firstFlow = new EventFlow();
                return new ScriptedTopic("start", Waiting());
            })
            .AddConversationRuntime("start");
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var session = scope.ServiceProvider.GetRequiredService<IConversationSession>();
        var subscription = runtime.Subscribe();
        await using var outputs = subscription.ReadAllAsync().GetAsyncEnumerator();

        var starting = runtime.StartAsync();
        await ReadUntilAsync<HostInteractionRequestOutput>(outputs);
        Assert.Single(session.PendingHostInteractionIds);

        await runtime.ResetAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => starting);

        Assert.Equal(2, activationCount);
        Assert.True(firstFlow!.IsTerminated);
        Assert.Empty(session.PendingHostInteractionIds);
        Assert.Equal("start", session.ActiveTopic?.TopicId);
    }

    [Fact]
    public async Task EventTriggerDemoTopic_UsesNonBlockingNotificationsAndCorrelatedInteraction()
    {
        var services = Services();
        new ConversaCoreBuilder(services)
            .AddTopic<EventTriggerDemoTopic>("event-trigger-demo", sp =>
                new EventTriggerDemoTopic(sp.GetRequiredService<ILogger<EventTriggerDemoTopic>>()))
            .AddConversationRuntime("event-trigger-demo");

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var subscription = runtime.Subscribe();
        await using var outputs = subscription.ReadAllAsync().GetAsyncEnumerator();

        var start = runtime.StartAsync();
        var notification = await ReadUntilAsync<HostNotificationOutput>(outputs);
        Assert.Equal("demo.progress", notification.EventName);
        Assert.False(start.IsCompleted);

        var request = await ReadUntilAsync<HostInteractionRequestOutput>(outputs);
        Assert.Equal("demo.confirm", request.InteractionName);
        await runtime.RespondToHostInteractionAsync(
            new HostInteractionResponse(request.RequestId, "approved"));
        await start;

        var completed = await ReadUntilAsync<TopicLifecycleOutput>(
            outputs,
            output => output.TopicId == "event-trigger-demo" &&
                      output.State == ConversationTopicState.Completed);
        Assert.Equal("event-trigger-demo", completed.TopicId);
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IConversationContext>(_ => new ConversationContext(
            Guid.NewGuid().ToString("N"), "subject", NullLogger<ConversationContext>.Instance));
        return services;
    }

    private static TopicResult Waiting() => new()
    {
        IsHandled = true,
        RequiresInput = true
    };

    private static TopicResult Completed() => new()
    {
        IsHandled = true,
        IsCompleted = true
    };

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

    private sealed class ScriptedTopic(string name, params TopicResult[] results) : ITopic, IDisposable
    {
        private readonly Queue<TopicResult> _results = new(results);
        public string Name { get; } = name;
        public int Priority => 0;
        public List<string> Messages { get; } = [];
        public bool Disposed { get; private set; }

        public Task<TopicResult> ProcessMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(message);
            return Task.FromResult(_results.Dequeue());
        }

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(1f);

        public void Dispose() => Disposed = true;
    }

    private sealed class CardFlow : ConversaCore.TopicFlow.TopicFlow
    {
        public CardFlow(ILogger<CardFlow> logger)
            : base(new TopicWorkflowContext(), logger, "Card flow")
        {
            Add(new AppointmentCardActivity(Context));
        }

        public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(1f);
    }

    private sealed class EventFlow : ConversaCore.TopicFlow.TopicFlow
    {
        public EventFlow()
            : base(new TopicWorkflowContext(), NullLogger.Instance, "Event flow")
        {
            Add(EventTriggerActivity.CreateWaitForResponse(
                "confirm",
                "site.confirm",
                "host_response",
                responseTimeout: TimeSpan.FromSeconds(10)));
        }

        public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(1f);
    }

    /// <summary>
    /// Framework-owned replacement for the historical EventTriggerDemoTopic sample.
    /// It deliberately exercises both host-output paths without depending on a domain app.
    /// </summary>
    private sealed class EventTriggerDemoTopic : ConversaCore.TopicFlow.TopicFlow
    {
        public EventTriggerDemoTopic(ILogger<EventTriggerDemoTopic> logger)
            : base(new TopicWorkflowContext(), logger, "event-trigger-demo")
        {
            Add(EventTriggerActivity.CreateFireAndForget(
                "demo.progress",
                new { Step = "notification" },
                logger: logger));
            Add(EventTriggerActivity.CreateWaitForResponse(
                "demo-confirm",
                "demo.confirm",
                "approval",
                new { Prompt = "Approve the demo continuation?" },
                responseTimeout: TimeSpan.FromSeconds(10),
                logger: logger));
            Add(new SimpleActivity("demo-complete", (context, _) =>
                Task.FromResult<object?>(context.GetValue<object>("approval"))));
        }

        public override Task<float> CanHandleAsync(
            string message,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(1f);
    }

    private sealed class AppointmentCardActivity(TopicWorkflowContext context)
        : AdaptiveCardActivity<AppointmentInput>(
            "appointment-card",
            context,
            NullLogger<AdaptiveCardActivity<AppointmentInput>>.Instance)
    {
        protected override string GetCardJson(TopicWorkflowContext context) =>
            "{\"type\":\"AdaptiveCard\",\"version\":\"1.5\",\"body\":[]}";
    }

    private sealed class AppointmentInput
    {
        [Required]
        public string Name { get; set; } = string.Empty;
    }
}
