using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using Xunit;

namespace ConversaCore.Tests.Authoring;

/// <summary>
/// Verifies package consumption and compiler compatibility (CC-607, WP6).
/// Compiles and executes generated-style source against packed ConversaCore and ConversaCore.UI
/// NuGet artifacts rather than repository project references or copied lib DLLs.
/// </summary>
public sealed class PackageConsumptionValidationTests
{
    private static readonly SemaphoreSlim PackagingLock = new(1, 1);

    [Fact]
    public void Package_NupkgMetadata_ContainsRequiredIdentityAndDependencies()
    {
        var repoRoot = FindRepositoryRoot();
        var packagesDir = Path.Combine(repoRoot, "artifacts", "packages");
        EnsurePackagesBuilt(repoRoot, packagesDir);

        var corePackage = Path.Combine(packagesDir, "ConversaCore.1.0.0.nupkg");
        var uiPackage = Path.Combine(packagesDir, "ConversaCore.UI.1.0.0.nupkg");

        Assert.True(File.Exists(corePackage), $"ConversaCore package not found at {corePackage}");
        Assert.True(File.Exists(uiPackage), $"ConversaCore.UI package not found at {uiPackage}");

        // 1. Verify ConversaCore nuspec
        using (var coreZip = ZipFile.OpenRead(corePackage))
        {
            var nuspecEntry = coreZip.Entries.FirstOrDefault(e => e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(nuspecEntry);
            using var reader = new StreamReader(nuspecEntry!.Open());
            var nuspecXml = XDocument.Load(reader);
            var id = nuspecXml.Descendants().FirstOrDefault(e => e.Name.LocalName == "id")?.Value;
            var version = nuspecXml.Descendants().FirstOrDefault(e => e.Name.LocalName == "version")?.Value;
            Assert.Equal("ConversaCore", id);
            Assert.Equal("1.0.0", version);
        }

        // 2. Verify ConversaCore.UI nuspec and dependency on ConversaCore 1.0.0
        using (var uiZip = ZipFile.OpenRead(uiPackage))
        {
            var nuspecEntry = uiZip.Entries.FirstOrDefault(e => e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(nuspecEntry);
            using var reader = new StreamReader(nuspecEntry!.Open());
            var nuspecXml = XDocument.Load(reader);
            var id = nuspecXml.Descendants().FirstOrDefault(e => e.Name.LocalName == "id")?.Value;
            var version = nuspecXml.Descendants().FirstOrDefault(e => e.Name.LocalName == "version")?.Value;
            Assert.Equal("ConversaCore.UI", id);
            Assert.Equal("1.0.0", version);

            var coreDependency = nuspecXml.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "dependency" && (string?)e.Attribute("id") == "ConversaCore");
            Assert.NotNull(coreDependency);
            Assert.Equal("1.0.0", (string?)coreDependency!.Attribute("version"));

            // Verify static web assets are packaged
            Assert.Contains(uiZip.Entries, e => e.FullName.Contains("chat.css", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(uiZip.Entries, e => e.FullName.Contains("chat-interop.js", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Package_GeneratedConsumer_RestoresBuildsAndExecutesWithZeroCopiedBinaries()
    {
        var repoRoot = FindRepositoryRoot();
        var packagesDir = Path.Combine(repoRoot, "artifacts", "packages");
        EnsurePackagesBuilt(repoRoot, packagesDir);

        var tempDirectory = Path.Combine(Path.GetTempPath(), "ConversaCore_ConsumerTest_" + Guid.NewGuid().ToString("N"));
        var nugetCacheDirectory = Path.Combine(tempDirectory, "nuget-cache");
        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(nugetCacheDirectory);

        try
        {
            // 1. Write nuget.config pointing to local package artifacts and isolated cache
            var nugetConfig = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""globalPackagesFolder"" value=""{nugetCacheDirectory}"" />
  </config>
  <packageSources>
    <clear />
    <add key=""LocalArtifacts"" value=""{packagesDir}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>";
            await File.WriteAllTextAsync(Path.Combine(tempDirectory, "nuget.config"), nugetConfig);

            // 2. Write standalone csproj referencing ONLY the packed NuGet packages (zero project references, zero copied DLLs)
            var projectFile = Path.Combine(tempDirectory, "GeneratedPackageConsumer.csproj");
            var csproj = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""ConversaCore"" Version=""1.0.0"" />
    <PackageReference Include=""ConversaCore.UI"" Version=""1.0.0"" />
    <PackageReference Include=""Microsoft.Extensions.DependencyInjection"" Version=""10.0.2"" />
    <PackageReference Include=""Microsoft.Extensions.Logging"" Version=""10.0.2"" />
    <PackageReference Include=""Microsoft.Extensions.Logging.Console"" Version=""10.0.2"" />
  </ItemGroup>
</Project>";
            await File.WriteAllTextAsync(projectFile, csproj);

            // 3. Write generated-style source code (ApplicantModel, ApplicantOnboardingTopic, Program)
            var programSource = @"using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Authoring;
using ConversaCore.Context;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using ConversaCore.TopicFlow.Core;
using ConversaCore.UI.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace GeneratedPackageConsumer;

public sealed class ApplicantModel
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string CoveragePlan { get; set; } = string.Empty;
}

public sealed class ApplicantOnboardingTopic : ComposedTopicFlow
{
    public const string TopicId = ""applicant.onboard"";
    public const string ConfirmCardId = ""applicant.confirm"";
    public const string InputCardId = ""applicant.card"";
    public const string CardContextKey = ""applicant.onboard.model"";
    private readonly IWorkflowActivityFactory _activities;

    public ApplicantOnboardingTopic(
        TopicWorkflowContext context,
        ILogger<ApplicantOnboardingTopic> logger,
        IWorkflowActivityFactory activities)
        : base(context, logger, TopicId)
    {
        _activities = activities;
    }

    protected override void ComposeWorkflow()
    {
        Add(_activities.CreatePrompt(new PromptActivityDefinition(
            ""applicant.prompt"",
            systemPrompt: ""You are an onboarding assistant."",
            userPromptTemplate: ""Welcome the applicant."")));

        Add(_activities.CreateQuickAnswer(new QuickAnswerActivityDefinition(
            ConfirmCardId,
            ""Would you like to begin onboarding now?"",
            new[] { ""Yes"", ""No"" },
            isRequired: true)));

        Add(_activities.CreateAdaptiveCard<ApplicantModel>(new GeneratedAdaptiveCardDefinition(
            InputCardId,
            new[]
            {
                new GeneratedAdaptiveCardFieldDefinition(
                    ""FullName"", ""Full Name"", GeneratedAdaptiveCardInputKind.Text, isRequired: true),
                new GeneratedAdaptiveCardFieldDefinition(
                    ""CoveragePlan"", ""Coverage Plan"", GeneratedAdaptiveCardInputKind.Choice, isRequired: true, choices: new[]
                    {
                        new GeneratedAdaptiveCardChoice(""Standard"", ""standard""),
                        new GeneratedAdaptiveCardChoice(""Premium"", ""premium"")
                    })
            },
            title: ""Applicant Information"",
            submitLabel: ""Submit Application"",
            modelContextKey: CardContextKey,
            customMessage: ""Please complete your details."",
            isRequired: true)));

        Add(new SimpleActivity(""applicant.complete"", ""Application recorded successfully.""));
    }
}

internal sealed class FakeChatCompletionService : IChatCompletionService
{
    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ChatMessageContent>>(
            new[] { new ChatMessageContent(AuthorRole.Assistant, ""Welcome to the applicant onboarding portal."") });
    }

    public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine(""[STARTING PACKAGE CONSUMER HARNESS]"");

        var activationsByConversation = new ConcurrentDictionary<string, List<ApplicantOnboardingTopic>>();
        await using var provider = BuildProvider(activationsByConversation);

        // ==========================================
        // Scenario 1: Single scope full execution
        // ==========================================
        {
            await using var scope = provider.CreateAsyncScope();
            var runtime = scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
            var context = scope.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
            var viewState = new ConversationOutputViewState();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await using var outputs = runtime.Subscribe().ReadAllAsync(cts.Token).GetAsyncEnumerator();

            await runtime.StartAsync(cts.Token);

            // Read confirm quick answer card
            var confirmCard = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, viewState,
                o => o.CardId == ApplicantOnboardingTopic.ConfirmCardId);

            // Answer 'Yes'
            await runtime.SubmitCardAsync(new CardSubmission(confirmCard.CardId, new Dictionary<string, object>
            {
                [""answer""] = ""Yes""
            }), cts.Token);

            // Read adaptive input card
            var inputCard = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs, viewState,
                o => o.CardId == ApplicantOnboardingTopic.InputCardId);

            // Submit card data
            await runtime.SubmitCardAsync(new CardSubmission(inputCard.CardId, new Dictionary<string, object>
            {
                [""FullName""] = ""Jane Doe"",
                [""CoveragePlan""] = ""premium""
            }), cts.Token);

            // Read completion
            await ReadAndApplyUntilAsync<TopicLifecycleOutput>(outputs, viewState,
                o => o.TopicId == ApplicantOnboardingTopic.TopicId && o.State == ConversationTopicState.Completed);

            var savedModel = context.GetValue<ApplicantModel>(ApplicantOnboardingTopic.CardContextKey);
            if (savedModel == null || savedModel.FullName != ""Jane Doe"" || savedModel.CoveragePlan != ""premium"")
                throw new Exception(""Saved model validation failed in single scope."");

            // Verify topic reset and recomposition via runtime
            await runtime.ResetAsync(cts.Token);

            // Read restarted confirm card from output
            var restartedCard = await ReadUntilAsync<AdaptiveCardOutput>(outputs,
                o => o.CardId == ApplicantOnboardingTopic.ConfirmCardId);
            if (restartedCard == null)
                throw new Exception(""Restarted card not received after ResetAsync."");

            var activations = activationsByConversation[runtime.ConversationId];
            if (activations.Count != 2)
                throw new Exception($""Expected 2 topic activations after reset, got {activations.Count}"");
            if (!activations[0].IsTerminated)
                throw new Exception(""First topic activation should be terminated after reset."");
            if (activations[1].IsTerminated)
                throw new Exception(""Second topic activation should be active after reset."");

            Console.WriteLine(""[PASS: SINGLE_SCOPE_LIFECYCLE]"");
        }

        // ==========================================
        // Scenario 2: Two concurrent scopes isolation
        // ==========================================
        {
            await using var scope1 = provider.CreateAsyncScope();
            await using var scope2 = provider.CreateAsyncScope();

            var runtime1 = scope1.ServiceProvider.GetRequiredService<IConversationRuntime>();
            var runtime2 = scope2.ServiceProvider.GetRequiredService<IConversationRuntime>();
            var context1 = scope1.ServiceProvider.GetRequiredService<TopicWorkflowContext>();
            var context2 = scope2.ServiceProvider.GetRequiredService<TopicWorkflowContext>();

            if (runtime1.ConversationId == runtime2.ConversationId)
                throw new Exception(""Conversation IDs must be distinct across concurrent scopes."");

            var view1 = new ConversationOutputViewState();
            var view2 = new ConversationOutputViewState();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await using var outputs1 = runtime1.Subscribe().ReadAllAsync(cts.Token).GetAsyncEnumerator();
            await using var outputs2 = runtime2.Subscribe().ReadAllAsync(cts.Token).GetAsyncEnumerator();

            await Task.WhenAll(runtime1.StartAsync(cts.Token), runtime2.StartAsync(cts.Token));

            var confirm1 = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs1, view1,
                o => o.CardId == ApplicantOnboardingTopic.ConfirmCardId);
            var confirm2 = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs2, view2,
                o => o.CardId == ApplicantOnboardingTopic.ConfirmCardId);

            await Task.WhenAll(
                runtime1.SubmitCardAsync(new CardSubmission(confirm1.CardId, new Dictionary<string, object> { [""answer""] = ""Yes"" }), cts.Token),
                runtime2.SubmitCardAsync(new CardSubmission(confirm2.CardId, new Dictionary<string, object> { [""answer""] = ""Yes"" }), cts.Token));

            var card1 = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs1, view1,
                o => o.CardId == ApplicantOnboardingTopic.InputCardId);
            var card2 = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputs2, view2,
                o => o.CardId == ApplicantOnboardingTopic.InputCardId);

            await Task.WhenAll(
                runtime1.SubmitCardAsync(new CardSubmission(card1.CardId, new Dictionary<string, object>
                {
                    [""FullName""] = ""Alice Smith"",
                    [""CoveragePlan""] = ""standard""
                }), cts.Token),
                runtime2.SubmitCardAsync(new CardSubmission(card2.CardId, new Dictionary<string, object>
                {
                    [""FullName""] = ""Bob Jones"",
                    [""CoveragePlan""] = ""premium""
                }), cts.Token));

            await Task.WhenAll(
                ReadAndApplyUntilAsync<TopicLifecycleOutput>(outputs1, view1, o => o.State == ConversationTopicState.Completed),
                ReadAndApplyUntilAsync<TopicLifecycleOutput>(outputs2, view2, o => o.State == ConversationTopicState.Completed));

            var topic1 = activationsByConversation[runtime1.ConversationId].Single();
            var topic2 = activationsByConversation[runtime2.ConversationId].Single();
            if (object.ReferenceEquals(topic1, topic2))
                throw new Exception(""Topic instances must not be shared across scopes."");

            var model1 = context1.GetValue<ApplicantModel>(ApplicantOnboardingTopic.CardContextKey);
            var model2 = context2.GetValue<ApplicantModel>(ApplicantOnboardingTopic.CardContextKey);

            if (model1?.FullName != ""Alice Smith"" || model2?.FullName != ""Bob Jones"")
                throw new Exception(""Model data leaked across concurrent scopes."");

            Console.WriteLine(""[PASS: TWO_CONCURRENT_SCOPES_ISOLATED]"");
        }

        // ==========================================
        // Scenario 3: Reset isolation across scopes
        // ==========================================
        {
            await using var scopeA = provider.CreateAsyncScope();
            await using var scopeB = provider.CreateAsyncScope();

            var runtimeA = scopeA.ServiceProvider.GetRequiredService<IConversationRuntime>();
            var runtimeB = scopeB.ServiceProvider.GetRequiredService<IConversationRuntime>();
            var contextB = scopeB.ServiceProvider.GetRequiredService<TopicWorkflowContext>();

            var viewA = new ConversationOutputViewState();
            var viewB = new ConversationOutputViewState();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await using var outputsA = runtimeA.Subscribe().ReadAllAsync(cts.Token).GetAsyncEnumerator();
            await using var outputsB = runtimeB.Subscribe().ReadAllAsync(cts.Token).GetAsyncEnumerator();

            await Task.WhenAll(runtimeA.StartAsync(cts.Token), runtimeB.StartAsync(cts.Token));

            var confirmA = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputsA, viewA,
                o => o.CardId == ApplicantOnboardingTopic.ConfirmCardId);
            var confirmB = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputsB, viewB,
                o => o.CardId == ApplicantOnboardingTopic.ConfirmCardId);

            await Task.WhenAll(
                runtimeA.SubmitCardAsync(new CardSubmission(confirmA.CardId, new Dictionary<string, object> { [""answer""] = ""Yes"" }), cts.Token),
                runtimeB.SubmitCardAsync(new CardSubmission(confirmB.CardId, new Dictionary<string, object> { [""answer""] = ""Yes"" }), cts.Token));

            var cardA = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputsA, viewA,
                o => o.CardId == ApplicantOnboardingTopic.InputCardId);
            var cardB = await ReadAndApplyUntilAsync<AdaptiveCardOutput>(outputsB, viewB,
                o => o.CardId == ApplicantOnboardingTopic.InputCardId);

            // Scope B is actively waiting at InputCardId. Now reset ONLY Scope A.
            await runtimeA.ResetAsync(cts.Token);

            var topicB = activationsByConversation[runtimeB.ConversationId].Single();
            if (topicB.IsTerminated)
                throw new Exception(""Scope B must NOT be terminated by Scope A reset."");

            // Scope B completes its card submit normally
            await runtimeB.SubmitCardAsync(new CardSubmission(cardB.CardId, new Dictionary<string, object>
            {
                [""FullName""] = ""Scope B Survivor"",
                [""CoveragePlan""] = ""premium""
            }), cts.Token);

            await ReadAndApplyUntilAsync<TopicLifecycleOutput>(outputsB, viewB, o => o.State == ConversationTopicState.Completed);

            var modelB = contextB.GetValue<ApplicantModel>(ApplicantOnboardingTopic.CardContextKey);
            if (modelB?.FullName != ""Scope B Survivor"")
                throw new Exception(""Scope B failed to finish cleanly after Scope A reset."");

            Console.WriteLine(""[PASS: RESET_ISOLATION]"");
        }

        Console.WriteLine(""[PASS: ALL_PACKAGE_CONSUMPTION_CHECKS_SUCCEEDED]"");
        return 0;
    }

    private static ServiceProvider BuildProvider(ConcurrentDictionary<string, List<ApplicantOnboardingTopic>> activations)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton<IChatCompletionService>(new FakeChatCompletionService());

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(kernelBuilder.Build());
        services.AddScoped<IConversationContext>(_ => new ConversationContext(
            Guid.NewGuid().ToString(""N""), ""test-subject"", NullLogger<ConversationContext>.Instance));

        new ConversaCoreBuilder(services)
            .AddTopic<ApplicantOnboardingTopic>(ApplicantOnboardingTopic.TopicId, sp =>
            {
                var topic = ActivatorUtilities.CreateInstance<ApplicantOnboardingTopic>(sp);
                var ctx = sp.GetRequiredService<IConversationContext>();
                activations.GetOrAdd(ctx.ConversationId, _ => new List<ApplicantOnboardingTopic>()).Add(topic);
                return topic;
            })
            .AddConversationRuntime(ApplicantOnboardingTopic.TopicId);

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
            if (outputs.Current is TOutput match && (predicate == null || predicate(match)))
                return match;
        }
        throw new InvalidOperationException($""Output {typeof(TOutput).Name} was not dispatched."");
    }

    private static async Task<TOutput> ReadUntilAsync<TOutput>(
        IAsyncEnumerator<ConversationOutput> outputs,
        Func<TOutput, bool>? predicate = null)
        where TOutput : ConversationOutput
    {
        while (await outputs.MoveNextAsync())
        {
            if (outputs.Current is TOutput match && (predicate == null || predicate(match)))
                return match;
        }
        throw new InvalidOperationException($""Output {typeof(TOutput).Name} was not dispatched."");
    }
}
";
            await File.WriteAllTextAsync(Path.Combine(tempDirectory, "Program.cs"), programSource);

            // 4. Restore and build standalone project against packages
            var restoreResult = await RunDotnetProcessAsync($"restore \"{projectFile}\"", tempDirectory);
            Assert.True(restoreResult.ExitCode == 0, $"dotnet restore failed:\n{restoreResult.Output}\n{restoreResult.Error}");

            var buildResult = await RunDotnetProcessAsync($"build \"{projectFile}\" --no-restore", tempDirectory);
            Assert.True(buildResult.ExitCode == 0, $"dotnet build failed:\n{buildResult.Output}\n{buildResult.Error}");

            var outputDll = Path.Combine(tempDirectory, "bin", "Debug", "net9.0", "GeneratedPackageConsumer.dll");
            Assert.True(File.Exists(outputDll), $"Compiled binary missing at {outputDll}");

            // 5. Execute compiled binary and verify runtime behavior
            var runResult = await RunDotnetProcessAsync($"run --project \"{projectFile}\" --no-build", tempDirectory);
            Assert.True(runResult.ExitCode == 0, $"dotnet run failed with code {runResult.ExitCode}:\n{runResult.Output}\n{runResult.Error}");

            Assert.Contains("[PASS: SINGLE_SCOPE_LIFECYCLE]", runResult.Output);
            Assert.Contains("[PASS: TWO_CONCURRENT_SCOPES_ISOLATED]", runResult.Output);
            Assert.Contains("[PASS: RESET_ISOLATION]", runResult.Output);
            Assert.Contains("[PASS: ALL_PACKAGE_CONSUMPTION_CHECKS_SUCCEEDED]", runResult.Output);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public async Task Package_BlazorTemplate_RestoresAndBuildsWithPackagesOnly_WhenLibDllsDeleted()
    {
        var repoRoot = FindRepositoryRoot();
        var packagesDir = Path.Combine(repoRoot, "artifacts", "packages");
        EnsurePackagesBuilt(repoRoot, packagesDir);

        var tempDirectory = Path.Combine(Path.GetTempPath(), "ConversaCore_TemplatePkgTest_" + Guid.NewGuid().ToString("N"));
        var nugetCacheDirectory = Path.Combine(tempDirectory, "nuget-cache");
        Directory.CreateDirectory(tempDirectory);
        Directory.CreateDirectory(nugetCacheDirectory);

        try
        {
            var templateSource = Path.Combine(repoRoot, "templates", "conversacore-blazor");
            Assert.True(Directory.Exists(templateSource), $"Template source not found at {templateSource}");

            // 1. Install template from local staged path
            var installResult = await RunDotnetProcessAsync($"new install \"{templateSource}\" --force", repoRoot);
            Assert.True(installResult.ExitCode == 0, $"dotnet new install failed: {installResult.Output}\n{installResult.Error}");

            // 2. Instantiate fresh project with --usePackages true
            var projectName = "PackagedBlazorApp";
            var instantiateResult = await RunDotnetProcessAsync(
                $"new conversacore-blazor -n {projectName} -o \"{tempDirectory}\" --usePackages true", repoRoot);
            Assert.True(instantiateResult.ExitCode == 0, $"dotnet new failed: {instantiateResult.Output}\n{instantiateResult.Error}");

            var projectFile = Path.Combine(tempDirectory, $"{projectName}.csproj");
            Assert.True(File.Exists(projectFile), $"Project file not created: {projectFile}");

            // 3. Write nuget.config pointing to local packages
            var nugetConfig = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""globalPackagesFolder"" value=""{nugetCacheDirectory}"" />
  </config>
  <packageSources>
    <clear />
    <add key=""LocalArtifacts"" value=""{packagesDir}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
</configuration>";
            await File.WriteAllTextAsync(Path.Combine(tempDirectory, "nuget.config"), nugetConfig);

            // 4. Verify project contains <ConversaCoreUsePackages>true</ConversaCoreUsePackages>
            var csprojContent = await File.ReadAllTextAsync(projectFile);
            Assert.Contains("<ConversaCoreUsePackages>true</ConversaCoreUsePackages>", csprojContent);

            // 5. CRITICAL VERIFICATION: Delete lib/ folder completely from instantiated project
            var libFolder = Path.Combine(tempDirectory, "lib");
            if (Directory.Exists(libFolder))
            {
                Directory.Delete(libFolder, recursive: true);
            }
            Assert.False(Directory.Exists(libFolder), "lib folder must be deleted to prove zero reliance on copied DLLs.");

            // 6. Restore and build project with zero copied DLLs
            var restoreResult = await RunDotnetProcessAsync($"restore \"{projectFile}\"", tempDirectory);
            Assert.True(restoreResult.ExitCode == 0, $"dotnet restore failed:\n{restoreResult.Output}\n{restoreResult.Error}");

            var buildResult = await RunDotnetProcessAsync($"build \"{projectFile}\" --no-restore", tempDirectory);
            Assert.True(buildResult.ExitCode == 0, $"dotnet build failed:\n{buildResult.Output}\n{buildResult.Error}");

            var outputDll = Path.Combine(tempDirectory, "bin", "Debug", "net9.0", $"{projectName}.dll");
            Assert.True(File.Exists(outputDll), $"Built binary does not exist at {outputDll}");
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private static void EnsurePackagesBuilt(string repoRoot, string packagesDir)
    {
        PackagingLock.Wait();
        try
        {
            Directory.CreateDirectory(packagesDir);
            var corePackage = Path.Combine(packagesDir, "ConversaCore.1.0.0.nupkg");
            var uiPackage = Path.Combine(packagesDir, "ConversaCore.UI.1.0.0.nupkg");

            if (File.Exists(corePackage) && File.Exists(uiPackage))
            {
                return;
            }

            var coreCsproj = Path.Combine(repoRoot, "ConversaCore", "ConversaCore.csproj");
            var uiCsproj = Path.Combine(repoRoot, "ConversaCore.UI", "ConversaCore.UI.csproj");

            var packCore = RunDotnetProcessAsync($"pack \"{coreCsproj}\" -c Release -o \"{packagesDir}\"", repoRoot).GetAwaiter().GetResult();
            if (packCore.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to pack ConversaCore: {packCore.Output}\n{packCore.Error}");
            }

            var packUi = RunDotnetProcessAsync($"pack \"{uiCsproj}\" -c Release -o \"{packagesDir}\"", repoRoot).GetAwaiter().GetResult();
            if (packUi.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to pack ConversaCore.UI: {packUi.Output}\n{packUi.Error}");
            }
        }
        finally
        {
            PackagingLock.Release();
        }
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

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        await process.WaitForExitAsync(cts.Token);

        var output = await outputTask;
        var error = await errorTask;

        return (process.ExitCode, output, error);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
