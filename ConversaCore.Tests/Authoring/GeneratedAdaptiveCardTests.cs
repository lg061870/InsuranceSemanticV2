using ConversaCore.Authoring;
using ConversaCore.Events;
using ConversaCore.Registration;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ConversaCore.Tests.Authoring;

public sealed class GeneratedAdaptiveCardTests
{
    [Fact]
    public async Task Render_UsesOnlyAllowlistedShapes_AndPreservesCorrelation()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var activity = scope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>()
            .CreateAdaptiveCard<GeneratedModel>(CreateDefinition());
        CardJsonEventArgs? sent = null;
        activity.CardJsonSent += (_, args) => sent = args;

        var result = await activity.RunAsync(context, null, CancellationToken.None);

        Assert.True(result.IsWaiting);
        Assert.NotNull(sent);
        Assert.Equal("profile", sent!.CardId);
        Assert.True(sent.IsRequired);

        using var json = JsonDocument.Parse(sent.CardJson);
        var root = json.RootElement;
        Assert.Equal("AdaptiveCard", root.GetProperty("type").GetString());
        Assert.Equal("1.3", root.GetProperty("version").GetString());
        Assert.Equal("profile", root.GetProperty("_metadata").GetProperty("activityId").GetString());
        Assert.Equal(
            ["Input.Text", "Input.Number", "Input.Date", "Input.Toggle", "Input.ChoiceSet"],
            root.GetProperty("body").EnumerateArray()
                .Where(element => element.GetProperty("type").GetString()!.StartsWith("Input.", StringComparison.Ordinal))
                .Select(element => element.GetProperty("type").GetString() ?? string.Empty)
                .ToArray());
        var action = Assert.Single(root.GetProperty("actions").EnumerateArray());
        Assert.Equal("Action.Submit", action.GetProperty("type").GetString());
        Assert.False(action.TryGetProperty("url", out _));
    }

    [Fact]
    public async Task Submission_BindsAndStoresTypedModel()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var activity = scope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>()
            .CreateAdaptiveCard<GeneratedModel>(CreateDefinition());
        GeneratedModel? bound = null;
        activity.ModelBoundTyped += (_, model) => bound = model;
        await activity.RunAsync(context, null, CancellationToken.None);

        activity.OnInputCollected(new AdaptiveCardInputCollectedEventArgs(ValidSubmission("private-value")));

        Assert.NotNull(bound);
        Assert.Equal("private-value", bound!.Name);
        Assert.Equal(42, bound.Age);
        Assert.True(bound.Accepted);
        Assert.Equal("plus", bound.Plan);
        Assert.Same(bound, context.GetValue<GeneratedModel>("typed-profile"));
    }

    [Fact]
    public async Task InvalidSubmission_RerendersWithCorrelatedValidationErrors()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var activity = scope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>()
            .CreateAdaptiveCard<GeneratedModel>(CreateDefinition());
        var validationFailures = 0;
        string? replacementJson = null;
        activity.ValidationFailed += (_, _) => validationFailures++;
        activity.CardJsonSent += (_, args) => replacementJson = args.CardJson;
        await activity.RunAsync(context, null, CancellationToken.None);

        var invalid = ValidSubmission(string.Empty);
        invalid["Age"] = "12";
        activity.OnInputCollected(new AdaptiveCardInputCollectedEventArgs(invalid));

        Assert.Equal(1, validationFailures);
        Assert.Equal(ActivityState.WaitingForUserInput, activity.CurrentState);
        Assert.Null(context.GetValue<GeneratedModel>("typed-profile"));
        Assert.NotNull(replacementJson);
        using var json = JsonDocument.Parse(replacementJson!);
        var errors = json.RootElement.GetProperty("validationErrors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.True(errors.TryGetProperty("age", out _));
    }

    [Fact]
    public async Task Reset_ReturnsGeneratedCardToRunnableCreatedState()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var activity = scope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>()
            .CreateAdaptiveCard<GeneratedModel>(CreateDefinition());
        var sentCount = 0;
        activity.CardJsonSent += (_, _) => sentCount++;

        await activity.RunAsync(context, null, CancellationToken.None);
        activity.Reset();
        Assert.Equal(ActivityState.Created, activity.CurrentState);
        await activity.RunAsync(context, null, CancellationToken.None);

        Assert.Equal(2, sentCount);
        Assert.Equal(ActivityState.WaitingForUserInput, activity.CurrentState);
    }

    [Fact]
    public async Task PreCancellation_StopsBeforeRendering()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var activity = scope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>()
            .CreateAdaptiveCard<GeneratedModel>(CreateDefinition());
        var sent = false;
        activity.CardJsonSent += (_, _) => sent = true;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            activity.RunAsync(context, null, cancellation.Token));

        Assert.False(sent);
        Assert.Equal(ActivityState.Failed, activity.CurrentState);
    }

    [Fact]
    public async Task SubmissionValues_AreNotWrittenToGeneratedCardLogs()
    {
        var loggerProvider = new CapturingLoggerProvider();
        using var provider = BuildProvider(loggerProvider);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var activity = scope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>()
            .CreateAdaptiveCard<GeneratedModel>(CreateDefinition());
        await activity.RunAsync(context, null, CancellationToken.None);

        activity.OnInputCollected(new AdaptiveCardInputCollectedEventArgs(
            ValidSubmission("secret-value-9321")));

        Assert.DoesNotContain(loggerProvider.Messages,
            message => message.Contains("secret-value-9321", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Factory_CreatesFreshGeneratedCardsPerConversationScope()
    {
        using var provider = BuildProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var definition = CreateDefinition();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var first = firstScope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>()
            .CreateAdaptiveCard<GeneratedModel>(definition);
        var second = secondScope.ServiceProvider.GetRequiredService<IWorkflowActivityFactory>()
            .CreateAdaptiveCard<GeneratedModel>(definition);

        Assert.NotSame(first, second);
        await first.RunAsync(firstContext, null, CancellationToken.None);
        first.OnInputCollected(new AdaptiveCardInputCollectedEventArgs(ValidSubmission("first")));

        Assert.NotNull(firstContext.GetValue<GeneratedModel>("typed-profile"));
        Assert.Null(secondContext.GetValue<GeneratedModel>("typed-profile"));
        Assert.Equal(ActivityState.Created, second.CurrentState);
    }

    [Fact]
    public void Definitions_RejectUnsupportedOrUnboundedShapes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeneratedAdaptiveCardFieldDefinition(
            "field", "Field", (GeneratedAdaptiveCardInputKind)999));
        Assert.Throws<ArgumentException>(() => new GeneratedAdaptiveCardFieldDefinition(
            "field", "Field", GeneratedAdaptiveCardInputKind.Choice));
        Assert.Throws<ArgumentException>(() => new GeneratedAdaptiveCardFieldDefinition(
            "field", "Field", GeneratedAdaptiveCardInputKind.Text,
            choices: [new GeneratedAdaptiveCardChoice("One", "1")]));
        Assert.Throws<ArgumentException>(() => new GeneratedAdaptiveCardDefinition(
            "card",
            [
                new GeneratedAdaptiveCardFieldDefinition("Name", "Name", GeneratedAdaptiveCardInputKind.Text),
                new GeneratedAdaptiveCardFieldDefinition("name", "Again", GeneratedAdaptiveCardInputKind.Text)
            ]));
        Assert.Throws<ArgumentException>(() => new GeneratedAdaptiveCardDefinition(
            "card",
            Enumerable.Range(0, GeneratedAdaptiveCardDefinition.MaximumFieldCount + 1)
                .Select(index => new GeneratedAdaptiveCardFieldDefinition(
                    $"field{index}", "Field", GeneratedAdaptiveCardInputKind.Text))));
        Assert.Throws<ArgumentException>(() => new GeneratedAdaptiveCardChoice(
            "Choice",
            new string('x', GeneratedAdaptiveCardDefinition.MaximumChoiceValueLength + 1)));
    }

    private static GeneratedAdaptiveCardDefinition CreateDefinition() => new(
        "profile",
        [
            new("Name", "Name", GeneratedAdaptiveCardInputKind.Text, true, "Full name"),
            new("Age", "Age", GeneratedAdaptiveCardInputKind.Number, true),
            new("BirthDate", "Birth date", GeneratedAdaptiveCardInputKind.Date),
            new("Accepted", "Accept terms", GeneratedAdaptiveCardInputKind.Toggle, true),
            new("Plan", "Plan", GeneratedAdaptiveCardInputKind.Choice, true, choices:
            [
                new GeneratedAdaptiveCardChoice("Basic", "basic"),
                new GeneratedAdaptiveCardChoice("Plus", "plus")
            ])
        ],
        title: "Profile",
        submitLabel: "Continue",
        modelContextKey: "typed-profile",
        customMessage: "Complete your profile",
        isRequired: true);

    private static Dictionary<string, object> ValidSubmission(string name) => new()
    {
        ["Name"] = name,
        ["Age"] = "42",
        ["BirthDate"] = "2000-01-02",
        ["Accepted"] = "true",
        ["Plan"] = "plus"
    };

    private static ServiceProvider BuildProvider(ILoggerProvider? loggerProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            if (loggerProvider is not null)
                builder.AddProvider(loggerProvider);
        });
        services.AddSingleton(Kernel.CreateBuilder().Build());
        services.AddScoped<TopicWorkflowContext>();
        new ConversaCoreBuilder(services).AddConversationRuntimeFoundation();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class GeneratedModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Range(18, 120)]
        public int Age { get; set; }

        public DateTime BirthDate { get; set; }
        public bool Accepted { get; set; }
        public string Plan { get; set; } = string.Empty;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);
        public void Dispose() { }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception));
        }
    }
}
