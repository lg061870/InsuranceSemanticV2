using System.Collections.Concurrent;
using System.Diagnostics;
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

public sealed class TemplateGeneratedConsumerValidationTests
{
    [Fact]
    public async Task Template_InstantiateAndBuild_SucceedsStandalone()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "ConversaCore_TemplateTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var repoRoot = FindRepositoryRoot();
            var templateSource = Path.Combine(repoRoot, "templates", "conversacore-blazor");
            Assert.True(Directory.Exists(templateSource), $"Template source not found at {templateSource}");

            // 1. Ensure template is installed from local staged path
            var installResult = await RunDotnetProcessAsync($"new install \"{templateSource}\" --force", repoRoot);
            Assert.True(installResult.ExitCode == 0, $"dotnet new install failed: {installResult.Output}\n{installResult.Error}");

            // 2. Instantiate fresh project
            var projectName = "StandaloneAgentTest";
            var instantiateResult = await RunDotnetProcessAsync(
                $"new conversacore-blazor -n {projectName} -o \"{tempDirectory}\"", repoRoot);
            Assert.True(instantiateResult.ExitCode == 0, $"dotnet new failed: {instantiateResult.Output}\n{instantiateResult.Error}");

            var projectFile = Path.Combine(tempDirectory, $"{projectName}.csproj");
            Assert.True(File.Exists(projectFile), $"Project file not created: {projectFile}");

            // 3. Verify domain tools were NOT excluded
            var toolsFolder = Path.Combine(tempDirectory, "Tools");
            Assert.True(Directory.Exists(toolsFolder), $"Tools folder not created: {toolsFolder}");
            Assert.True(File.Exists(Path.Combine(toolsFolder, "SampleLookupTool.cs")), "SampleLookupTool.cs missing from instantiated project.");
            Assert.True(File.Exists(Path.Combine(toolsFolder, "SampleOrderTool.cs")), "SampleOrderTool.cs missing from instantiated project.");

            // 4. Verify build-template script was excluded
            Assert.False(File.Exists(Path.Combine(toolsFolder, "build-template.ps1")), "build-template.ps1 should be excluded from instantiated project.");

            // 5. Build instantiated project standalone (using lib\ConversaCore.dll references)
            var buildResult = await RunDotnetProcessAsync($"build \"{projectFile}\"", tempDirectory);
            Assert.True(buildResult.ExitCode == 0, $"dotnet build failed: {buildResult.Output}\n{buildResult.Error}");

            var outputDll = Path.Combine(tempDirectory, "bin", "Debug", "net9.0", $"{projectName}.dll");
            Assert.True(File.Exists(outputDll), $"Built binary does not exist at {outputDll}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup of temp directory
            }
        }
    }

    [Fact]
    public async Task Template_TwoConcurrentScopes_IsolateTopicActivityCardAndWorkflowState()
    {
        var activationsByConversation = new ConcurrentDictionary<string, List<SampleTopic>>();
        await using var provider = BuildProvider(activationsByConversation);
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();

        var firstRuntime = firstScope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var secondRuntime = secondScope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();

        var firstView = new ConversationOutputViewState();
        var secondView = new ConversationOutputViewState();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var firstOutputs = firstRuntime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();
        await using var secondOutputs = secondRuntime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        // 1. Start both runtimes concurrently
        await Task.WhenAll(firstRuntime.StartAsync(timeout.Token), secondRuntime.StartAsync(timeout.Token));

        // 2. Both receive confirmation quick answers
        var firstConfirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(firstOutputs, firstView,
            output => output.CardId == SampleTopic.ConfirmActivityId);
        var secondConfirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(secondOutputs, secondView,
            output => output.CardId == SampleTopic.ConfirmActivityId);

        Assert.Equal(SampleTopic.ConfirmActivityId, firstConfirm.CardId);
        Assert.Equal(SampleTopic.ConfirmActivityId, secondConfirm.CardId);

        // 3. Both submit affirmative "Yes"
        await Task.WhenAll(
            firstRuntime.SubmitCardAsync(new CardSubmission(firstConfirm.CardId,
                new Dictionary<string, object> { ["answer"] = "Yes" }), timeout.Token),
            secondRuntime.SubmitCardAsync(new CardSubmission(secondConfirm.CardId,
                new Dictionary<string, object> { ["answer"] = "Yes" }), timeout.Token));

        // 4. Both receive typed input cards
        var firstInputCard = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(firstOutputs, firstView,
            output => output.CardId == SampleTopic.InputCardActivityId);
        var secondInputCard = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(secondOutputs, secondView,
            output => output.CardId == SampleTopic.InputCardActivityId);

        // 5. Submit distinct models in each scope
        const string firstQuestion = "First Scope: How does ConversaCore manage state?";
        const string secondQuestion = "Second Scope: How do tools execute deterministically?";

        await Task.WhenAll(
            firstRuntime.SubmitCardAsync(new CardSubmission(firstInputCard.CardId,
                new Dictionary<string, object> { [nameof(SampleInputModel.Question)] = firstQuestion }), timeout.Token),
            secondRuntime.SubmitCardAsync(new CardSubmission(secondInputCard.CardId,
                new Dictionary<string, object> { [nameof(SampleInputModel.Question)] = secondQuestion }), timeout.Token));

        // 6. Wait for topic completion in both scopes
        await Task.WhenAll(
            ReadAndApplyUntilAsync<TopicLifecycleOutput>(firstOutputs, firstView,
                output => output.TopicId == ConversaCoreTopicRegistration.SampleTopicId &&
                          output.State == ConversationTopicState.Completed),
            ReadAndApplyUntilAsync<TopicLifecycleOutput>(secondOutputs, secondView,
                output => output.TopicId == ConversaCoreTopicRegistration.SampleTopicId &&
                          output.State == ConversationTopicState.Completed));

        // 7. Assert strict isolation
        Assert.NotEqual(firstRuntime.ConversationId, secondRuntime.ConversationId);

        // Assert 2 distinct topic instances were constructed, each correlated to its conversation
        var firstTopic = Assert.Single(activationsByConversation[firstRuntime.ConversationId]);
        var secondTopic = Assert.Single(activationsByConversation[secondRuntime.ConversationId]);
        Assert.NotSame(firstTopic, secondTopic);

        // Assert activity instances are not shared across scopes
        var firstActivities = firstTopic.GetAllActivities().ToList();
        var secondActivities = secondTopic.GetAllActivities().ToList();
        Assert.Equal(firstActivities.Count, secondActivities.Count);
        for (var i = 0; i < firstActivities.Count; i++)
        {
            Assert.NotSame(firstActivities[i], secondActivities[i]);
        }

        // Assert context state isolation
        var firstModel = firstContext.GetValue<SampleInputModel>(SampleTopic.InputModelContextKey);
        var secondModel = secondContext.GetValue<SampleInputModel>(SampleTopic.InputModelContextKey);
        Assert.NotNull(firstModel);
        Assert.NotNull(secondModel);
        Assert.Equal(firstQuestion, firstModel!.Question);
        Assert.Equal(secondQuestion, secondModel!.Question);

        // Assert view state isolation
        Assert.Contains(firstView.Messages, m => m.Content.Contains(firstQuestion, StringComparison.Ordinal));
        Assert.DoesNotContain(firstView.Messages, m => m.Content.Contains(secondQuestion, StringComparison.Ordinal));
        Assert.Contains(secondView.Messages, m => m.Content.Contains(secondQuestion, StringComparison.Ordinal));
        Assert.DoesNotContain(secondView.Messages, m => m.Content.Contains(firstQuestion, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Template_ConcurrentScopes_ResetOneScope_PreservesOtherScope()
    {
        var activationsByConversation = new ConcurrentDictionary<string, List<SampleTopic>>();
        await using var provider = BuildProvider(activationsByConversation);
        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();

        var firstRuntime = firstScope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var secondRuntime = secondScope.ServiceProvider.GetRequiredService<IConversationRuntime>();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();

        var firstView = new ConversationOutputViewState();
        var secondView = new ConversationOutputViewState();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var firstOutputs = firstRuntime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();
        await using var secondOutputs = secondRuntime.Subscribe().ReadAllAsync(timeout.Token).GetAsyncEnumerator();

        await Task.WhenAll(firstRuntime.StartAsync(timeout.Token), secondRuntime.StartAsync(timeout.Token));

        var firstConfirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(firstOutputs, firstView,
            output => output.CardId == SampleTopic.ConfirmActivityId);
        var secondConfirm = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(secondOutputs, secondView,
            output => output.CardId == SampleTopic.ConfirmActivityId);

        await Task.WhenAll(
            firstRuntime.SubmitCardAsync(new CardSubmission(firstConfirm.CardId,
                new Dictionary<string, object> { ["answer"] = "Yes" }), timeout.Token),
            secondRuntime.SubmitCardAsync(new CardSubmission(secondConfirm.CardId,
                new Dictionary<string, object> { ["answer"] = "Yes" }), timeout.Token));

        var firstInputCard = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(firstOutputs, firstView,
            output => output.CardId == SampleTopic.InputCardActivityId);
        var secondInputCard = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(secondOutputs, secondView,
            output => output.CardId == SampleTopic.InputCardActivityId);

        const string secondQuestion = "Persistent question in Scope 2";

        // Scope 1 submits its question and completes
        await firstRuntime.SubmitCardAsync(new CardSubmission(firstInputCard.CardId,
            new Dictionary<string, object> { [nameof(SampleInputModel.Question)] = "Transient question in Scope 1" }), timeout.Token);

        await ReadAndApplyUntilAsync<TopicLifecycleOutput>(firstOutputs, firstView,
            output => output.State == ConversationTopicState.Completed);

        // Scope 2 is waiting at the card prompt (active, not terminated)
        var secondTopic = Assert.Single(activationsByConversation[secondRuntime.ConversationId]);
        Assert.False(secondTopic.IsTerminated, "Scope 2 should be active waiting for user input.");

        // Now RESET only the first scope
        await firstRuntime.ResetAsync(timeout.Token);

        // Read restarted output from first scope
        var restartedConfirm = await ReadUntilAsync<AdaptiveCardOutput>(firstOutputs,
            output => output.CardId == SampleTopic.ConfirmActivityId);
        Assert.Equal(SampleTopic.ConfirmActivityId, restartedConfirm.CardId);

        var firstScopeActivations = activationsByConversation[firstRuntime.ConversationId];
        var secondScopeActivations = activationsByConversation[secondRuntime.ConversationId];

        // Scope 1 had 2 activations: original terminated by reset, restarted active
        Assert.Equal(2, firstScopeActivations.Count);
        Assert.True(firstScopeActivations[0].IsTerminated, "Scope 1 original topic should be terminated.");
        Assert.False(firstScopeActivations[1].IsTerminated, "Scope 1 restarted topic should be active.");

        // Scope 1 model context was cleared
        Assert.Null(firstContext.GetValue<SampleInputModel>(SampleTopic.InputModelContextKey));

        // Assert Scope 2 was completely unimpacted by Scope 1's reset:
        // 1. Scope 2 topic is STILL the single instance, still active, and NOT terminated
        Assert.Same(secondTopic, Assert.Single(secondScopeActivations));
        Assert.False(secondTopic.IsTerminated, "Scope 2 topic must remain active and unaffected by Scope 1 reset.");

        // 2. Scope 2 proceeds to submit its card and complete normally
        await secondRuntime.SubmitCardAsync(new CardSubmission(secondInputCard.CardId,
            new Dictionary<string, object> { [nameof(SampleInputModel.Question)] = secondQuestion }), timeout.Token);

        await ReadAndApplyUntilAsync<TopicLifecycleOutput>(secondOutputs, secondView,
            output => output.State == ConversationTopicState.Completed);

        var secondModel = secondContext.GetValue<SampleInputModel>(SampleTopic.InputModelContextKey);
        Assert.NotNull(secondModel);
        Assert.Equal(secondQuestion, secondModel!.Question);
        Assert.Contains(secondView.Messages, m => m.Content.Contains(secondQuestion, StringComparison.Ordinal));
        Assert.DoesNotContain(secondView.Messages, m => m.Content.Contains("Transient question", StringComparison.Ordinal));
    }

    private static ServiceProvider BuildProvider(ConcurrentDictionary<string, List<SampleTopic>> activationsByConversation)
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
                var context = serviceProvider.GetRequiredService<IConversationContext>();
                activationsByConversation.GetOrAdd(context.ConversationId, _ => new List<SampleTopic>()).Add(topic);
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

    private static string FindRepositoryRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "InsuranceSemanticV2.sln")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not find repository root containing InsuranceSemanticV2.sln");
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunDotnetProcessAsync(string arguments, string workingDirectory)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(cts.Token);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
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
