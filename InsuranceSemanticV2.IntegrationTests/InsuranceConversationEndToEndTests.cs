using System.Collections.Concurrent;
using System.Threading.Channels;
using ConversaCore.Context;
using ConversaCore.Interfaces;
using ConversaCore.Models;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.Tools;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using FluentAssertions;
using InsuranceAgent.Contracts;
using InsuranceAgent.DomainTypes;
using InsuranceAgent.Repositories;
using InsuranceAgent.Tools;
using InsuranceAgent.Topics;
using InsuranceAgent.Topics.MarketingTypeTopics;
using InsuranceSemanticV2.Core.DTO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;

namespace InsuranceSemanticV2.IntegrationTests;

public sealed class InsuranceConversationEndToEndTests
{
    private static readonly string[] ExpectedPersistenceTools =
    [
        "insurance.lead.create",
        "insurance.profile.contact.save",
        "insurance.profile.life-goals.save",
        "insurance.profile.health.save",
        "insurance.profile.coverage.save",
        "insurance.profile.dependents.save",
        "insurance.profile.employment.save",
        "insurance.profile.beneficiaries.save"
    ];

    [Fact]
    public async Task FullQualification_PersistsEverySection_NotifiesHost_AndHandsOffQualifiedLead()
    {
        await using var fixture = CreateFixture(qualificationScore: 88);

        await fixture.RunFullQualificationAsync();

        fixture.Tools.ToolIds.Should().ContainInOrder(ExpectedPersistenceTools);
        fixture.Tools.ToolIds.Should().ContainSingle(id => id == "insurance.lead.handoff");
        var handoff = fixture.Tools.Requests.OfType<QualifiedLeadHandoffRequest>()
            .Should().ContainSingle().Which;
        handoff.LeadId.Should().Be(42);
        handoff.QualificationScore.Should().Be(88);
        fixture.Outputs.OfType<HostNotification<InsuranceCustomerConsoleNotification>>()
            .Should().ContainSingle(output => output.EventName == "customer_console_show");
        fixture.Outputs.OfType<HostNotification<InsuranceQualificationNotification>>()
            .Should().ContainSingle(output => output.EventName == "qualification_complete");
    }

    [Fact]
    public async Task FullQualification_BelowThreshold_PersistsLeadButSkipsLiveAgentHandoff()
    {
        await using var fixture = CreateFixture(qualificationScore: 55);

        await fixture.RunFullQualificationAsync();

        fixture.Tools.ToolIds.Should().ContainInOrder(ExpectedPersistenceTools);
        fixture.Tools.ToolIds.Should().NotContain("insurance.lead.handoff");
        fixture.Outputs.OfType<HostNotification<InsuranceQualificationNotification>>()
            .Should().ContainSingle(output => output.EventName == "qualification_complete");
    }

    [Theory]
    [InlineData("78701", "yes", null, InsuranceTopicIds.MarketingT1)]
    [InlineData("90210", "yes", "yes", InsuranceTopicIds.MarketingT1)]
    [InlineData("90210", "yes", "no", InsuranceTopicIds.MarketingT2)]
    [InlineData("78701", "no", null, InsuranceTopicIds.MarketingT3)]
    public async Task ConsentComposition_RoutesToExpectedMarketingTopic(
        string zipCode,
        string tcpaConsent,
        string? ccpaAcknowledgment,
        string expectedTopicId)
    {
        await using var fixture = CreateConsentFixture();

        var run = fixture.Runtime.StartAsync();
        var compliance = await fixture.ReadCardAsync();
        compliance.CardId.Should().Be(ComplianceTopic.ActivityId_ShowCard);
        await fixture.Runtime.SubmitCardAsync(new CardSubmission(compliance.CardId,
            new Dictionary<string, object>
            {
                ["zip_code"] = zipCode,
                ["tcpa_consent"] = tcpaConsent
            }));

        if (ccpaAcknowledgment is not null)
        {
            var california = await fixture.ReadCardAsync();
            california.CardId.Should().Be("insurance.ca-residency");
            await fixture.Runtime.SubmitCardAsync(new CardSubmission(california.CardId,
                new Dictionary<string, object>
                {
                    ["zip_code"] = zipCode,
                    ["ccpa_acknowledgment"] = ccpaAcknowledgment
                }));
        }

        await run.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Branches.ActivatedTopicIds.Should().ContainSingle().Which.Should().Be(expectedTopicId);
    }

    [Fact]
    public async Task ConsentReset_RecomposesStartAndCompliance_AtTheFirstCard()
    {
        await using var fixture = CreateConsentFixture();

        await fixture.Runtime.StartAsync();
        var first = await fixture.ReadCardAsync();
        fixture.ResetObservedCards();
        await fixture.Runtime.ResetAsync();
        var restarted = await fixture.ReadCardAsync();

        first.CardId.Should().Be(ComplianceTopic.ActivityId_ShowCard);
        restarted.CardId.Should().Be(first.CardId);
        fixture.Branches.ActivatedTopicIds.Should().BeEmpty();
    }

    [Fact]
    public async Task PartialConsentTopic_ComposesAfterActivation_AndEmitsTypedHostNotifications()
    {
        await using var fixture = CreateMarketingT2Fixture();

        await fixture.Runtime.StartAsync();
        var card = await fixture.ReadCardAsync();
        await fixture.Runtime.SubmitCardAsync(new CardSubmission(card.CardId,
            new Dictionary<string, object>
            {
                ["language"] = "English",
                ["lead_source"] = "Website",
                ["interest_level"] = "Medium",
                ["lead_intent"] = "Compare"
            }));
        await fixture.WaitForOutputAsync("qualification_complete");

        card.CardId.Should().Be(MarketingT2Topic.ActivityId_LeadDetails);
        fixture.Outputs.OfType<HostNotification<InsuranceCustomerConsoleNotification>>()
            .Should().ContainSingle(output => output.EventName == "customer_console_show");
        fixture.Outputs.OfType<HostNotification<InsuranceProgressNotification>>()
            .Should().ContainSingle(output => output.EventName == "lead_details_submitted");
        fixture.Outputs.OfType<HostNotification<InsuranceQualificationNotification>>()
            .Should().ContainSingle(output => output.EventName == "qualification_complete");
    }

    [Fact]
    public async Task ResetWhileWaiting_RestartsAtFirstInsuranceCard_WithoutPersistingStaleInput()
    {
        await using var fixture = CreateFixture(qualificationScore: 88);

        await fixture.Runtime.StartAsync();
        var first = await fixture.ReadNextCardAsync();
        await fixture.Runtime.ResetAsync();
        var restarted = await fixture.ReadNextCardAsync();

        first.CardId.Should().Be("ContactInfo");
        restarted.CardId.Should().Be(first.CardId);
        fixture.Tools.ToolIds.Should().BeEmpty();
    }

    [Fact]
    public async Task PersistenceFailure_StopsQualificationBeforeLaterWritesOrHostCompletion()
    {
        await using var fixture = CreateFixture(
            qualificationScore: 88,
            failedToolId: "insurance.profile.health.save");

        await fixture.DriveQualificationCardsAsync(cardCount: 4);

        fixture.Tools.ToolIds.Should().Contain("insurance.profile.health.save");
        fixture.Tools.ToolIds.Should().NotContain("insurance.profile.coverage.save");
        fixture.Tools.ToolIds.Should().NotContain("insurance.lead.handoff");
        fixture.Outputs.OfType<HostNotificationOutput>()
            .Should().NotContain(output => output.EventName == "qualification_complete");
    }

    [Fact]
    public async Task UnmatchedMessage_InterruptsInsuranceFlow_WithFallback_ThenResumesIt()
    {
        await using var fixture = CreateFallbackFixture();

        await fixture.Runtime.StartAsync();
        await fixture.Runtime.SendMessageAsync("question outside the qualification flow");

        fixture.Probe.FallbackMessages.Should().ContainSingle()
            .Which.Should().Be("question outside the qualification flow");
        fixture.Probe.ActiveMessages.Should().Equal(string.Empty, "question outside the qualification flow",
            "Interrupted topic completed");
    }

    [Fact]
    public async Task TwoConcurrentCircuits_IsolateIdentityInputToolsAndOutputs()
    {
        await using var provider = CreateTwoCircuitProvider();
        await using var first = new InsuranceRuntimeFixture(provider, ownsProvider: false);
        await using var second = new InsuranceRuntimeFixture(provider, ownsProvider: false);
        var firstData = CardData("Ada Circuit", "ada.circuit@example.com", "Grace Circuit");
        var secondData = CardData("Linus Circuit", "linus.circuit@example.com", "Tove Circuit");

        await Task.WhenAll(
            first.RunFullQualificationAsync(firstData),
            second.RunFullQualificationAsync(secondData));

        first.Runtime.ConversationId.Should().NotBe(second.Runtime.ConversationId);
        first.Tools.LeadId.Should().NotBe(second.Tools.LeadId);
        first.Tools.Requests.OfType<CreateLeadRequest>().Should().ContainSingle()
            .Which.ContactInfo!.FullName.Should().Be("Ada Circuit");
        second.Tools.Requests.OfType<CreateLeadRequest>().Should().ContainSingle()
            .Which.ContactInfo!.FullName.Should().Be("Linus Circuit");
        first.Tools.Requests.OfType<SaveBeneficiariesRequest>().Should().ContainSingle()
            .Which.Model.BeneficiaryName.Should().Be("Grace Circuit");
        second.Tools.Requests.OfType<SaveBeneficiariesRequest>().Should().ContainSingle()
            .Which.Model.BeneficiaryName.Should().Be("Tove Circuit");
        first.Tools.Requests.OfType<QualifiedLeadHandoffRequest>().Should().ContainSingle()
            .Which.LeadId.Should().Be(first.Tools.LeadId);
        second.Tools.Requests.OfType<QualifiedLeadHandoffRequest>().Should().ContainSingle()
            .Which.LeadId.Should().Be(second.Tools.LeadId);
        first.Outputs.Should().OnlyContain(output => output.ConversationId == first.Runtime.ConversationId);
        second.Outputs.Should().OnlyContain(output => output.ConversationId == second.Runtime.ConversationId);
    }

    [Fact]
    public async Task ResettingOneCircuit_DoesNotCancelOrMutateConcurrentCircuit()
    {
        await using var provider = CreateTwoCircuitProvider();
        await using var resetting = new InsuranceRuntimeFixture(provider, ownsProvider: false);
        await using var completing = new InsuranceRuntimeFixture(provider, ownsProvider: false);

        await resetting.Runtime.StartAsync();
        await resetting.ReadNextCardAsync();
        await Task.WhenAll(
            resetting.Runtime.ResetAsync(),
            completing.RunFullQualificationAsync(
                CardData("Independent Circuit", "independent@example.com", "Separate Beneficiary")));

        resetting.Tools.ToolIds.Should().BeEmpty();
        completing.Tools.ToolIds.Should().ContainInOrder(ExpectedPersistenceTools);
        completing.Outputs.OfType<HostNotification<InsuranceQualificationNotification>>()
            .Should().ContainSingle(output => output.EventName == "qualification_complete");
        completing.Tools.Requests.OfType<CreateLeadRequest>().Should().ContainSingle()
            .Which.ContactInfo!.FullName.Should().Be("Independent Circuit");
    }

    private static InsuranceRuntimeFixture CreateFixture(int qualificationScore, string? failedToolId = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddOptions();
        services.AddScoped<TopicWorkflowContext>();
        services.AddScoped<IConversationContext>(provider => new ConversationContext(
            Guid.NewGuid().ToString("N"),
            "insurance-e2e",
            provider.GetRequiredService<ILogger<ConversationContext>>()));

        var vectorDatabase = new EmptyVectorDatabaseService();
        services.AddSingleton<IVectorDatabaseService>(vectorDatabase);
        services.AddSingleton<InsuranceRuleRepository>();
        services.AddSingleton(CreateKernel(qualificationScore));
        services.AddScoped(_ => new RecordingToolExecutor(failedToolId));
        services.AddScoped<IToolExecutor>(provider => provider.GetRequiredService<RecordingToolExecutor>());

        new ConversaCoreBuilder(services)
            .AddTopic<MarketingT1Topic>(
                InsuranceTopicIds.MarketingT1,
                options => options.AllowedToolIds = ExpectedPersistenceTools
                    .Append("insurance.lead.handoff")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase))
            .AddConversationRuntime(InsuranceTopicIds.MarketingT1);

        return new InsuranceRuntimeFixture(services.BuildServiceProvider(validateScopes: true));
    }

    private static ServiceProvider CreateTwoCircuitProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddOptions();
        services.AddScoped<TopicWorkflowContext>();
        services.AddScoped<IConversationContext>(provider => new ConversationContext(
            Guid.NewGuid().ToString("N"),
            "insurance-two-circuit-e2e",
            provider.GetRequiredService<ILogger<ConversationContext>>()));
        services.AddSingleton<IVectorDatabaseService>(new EmptyVectorDatabaseService());
        services.AddSingleton<InsuranceRuleRepository>();
        services.AddSingleton(CreateKernel(88));
        services.AddSingleton<CircuitLeadIdSource>();
        services.AddScoped(provider => new RecordingToolExecutor(
            leadId: provider.GetRequiredService<CircuitLeadIdSource>().Next()));
        services.AddScoped<IToolExecutor>(provider => provider.GetRequiredService<RecordingToolExecutor>());

        new ConversaCoreBuilder(services)
            .AddTopic<MarketingT1Topic>(
                InsuranceTopicIds.MarketingT1,
                options => options.AllowedToolIds = ExpectedPersistenceTools
                    .Append("insurance.lead.handoff")
                    .ToHashSet(StringComparer.OrdinalIgnoreCase))
            .AddConversationRuntime(InsuranceTopicIds.MarketingT1);
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static IReadOnlyDictionary<string, object> CardData(
        string fullName,
        string email,
        string beneficiaryName)
    {
        var data = new Dictionary<string, object>(ValidCardData, StringComparer.OrdinalIgnoreCase)
        {
            ["full_name"] = fullName,
            ["email_address"] = email,
            ["beneficiary_name"] = beneficiaryName
        };
        return data;
    }

    private static ConsentRuntimeFixture CreateConsentFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddOptions();
        services.AddScoped<TopicWorkflowContext>();
        services.AddScoped<IConversationContext>(provider => new ConversationContext(
            Guid.NewGuid().ToString("N"),
            "insurance-consent-e2e",
            provider.GetRequiredService<ILogger<ConversationContext>>()));

        var branches = new BranchRecorder();
        services.AddSingleton(branches);
        new ConversaCoreBuilder(services)
            .AddTopic<InsuranceConversationStartTopic>(InsuranceTopicIds.ConversationStart)
            .AddTopic<ComplianceTopic>("ComplianceTopic")
            .AddTopic<BranchProbeTopic>(InsuranceTopicIds.MarketingT1,
                provider => new BranchProbeTopic(
                    provider.GetRequiredService<TopicWorkflowContext>(),
                    provider.GetRequiredService<ILogger<BranchProbeTopic>>(),
                    provider.GetRequiredService<BranchRecorder>(),
                    InsuranceTopicIds.MarketingT1))
            .AddTopic<BranchProbeTopic>(InsuranceTopicIds.MarketingT2,
                provider => new BranchProbeTopic(
                    provider.GetRequiredService<TopicWorkflowContext>(),
                    provider.GetRequiredService<ILogger<BranchProbeTopic>>(),
                    provider.GetRequiredService<BranchRecorder>(),
                    InsuranceTopicIds.MarketingT2))
            .AddTopic<BranchProbeTopic>(InsuranceTopicIds.MarketingT3,
                provider => new BranchProbeTopic(
                    provider.GetRequiredService<TopicWorkflowContext>(),
                    provider.GetRequiredService<ILogger<BranchProbeTopic>>(),
                    provider.GetRequiredService<BranchRecorder>(),
                    InsuranceTopicIds.MarketingT3))
            .AddConversationRuntime(InsuranceTopicIds.ConversationStart);

        return new ConsentRuntimeFixture(services.BuildServiceProvider(validateScopes: true), branches);
    }

    private static MarketingT2RuntimeFixture CreateMarketingT2Fixture()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddOptions();
        services.AddScoped<IConversationContext>(provider => new ConversationContext(
            Guid.NewGuid().ToString("N"),
            "insurance-t2-authoring-e2e",
            provider.GetRequiredService<ILogger<ConversationContext>>()));

        new ConversaCoreBuilder(services)
            .AddTopic<MarketingT2Topic>(InsuranceTopicIds.MarketingT2)
            .AddConversationRuntime(InsuranceTopicIds.MarketingT2);

        return new MarketingT2RuntimeFixture(services.BuildServiceProvider(validateScopes: true));
    }

    private static FallbackRuntimeFixture CreateFallbackFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddOptions();
        services.AddScoped<IConversationContext>(provider => new ConversationContext(
            Guid.NewGuid().ToString("N"),
            "insurance-fallback-e2e",
            provider.GetRequiredService<ILogger<ConversationContext>>()));
        var probe = new FallbackProbeState();
        services.AddSingleton(probe);

        new ConversaCoreBuilder(services)
            .AddTopic<DecliningInsuranceTopic>("insurance.e2e.active",
                provider => new DecliningInsuranceTopic(provider.GetRequiredService<FallbackProbeState>()))
            .AddTopic<FallbackProbeTopic>("system.fallback",
                provider => new FallbackProbeTopic(provider.GetRequiredService<FallbackProbeState>()),
                options => options.Classification = TopicClassification.System)
            .AddConversationRuntime("insurance.e2e.active", new TopicRouterOptions
            {
                FallbackTopicId = "system.fallback"
            });

        return new FallbackRuntimeFixture(services.BuildServiceProvider(validateScopes: true), probe);
    }

    private static Kernel CreateKernel(int qualificationScore)
    {
        var result = new QualifiedCarriers
        {
            Carriers =
            [
                new CarrierEligibility
                {
                    CarrierName = "Test Carrier",
                    Eligibility = "Eligible",
                    MatchScore = qualificationScore,
                    ConfidenceScore = 0.95
                }
            ]
        }.ToJson();

        var completion = new Mock<IChatCompletionService>();
        completion
            .Setup(service => service.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings?>(),
                It.IsAny<Kernel?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ChatMessageContent(AuthorRole.Assistant, result)]);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(completion.Object);
        return builder.Build();
    }

    private static IReadOnlyDictionary<string, object> ValidCardData { get; } =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["full_name"] = "Ada Prospect",
            ["date_of_birth"] = "1990-01-02",
            ["phone_number"] = "555-010-1234",
            ["email_address"] = "ada@example.com",
            ["street_address"] = "1 Main Street",
            ["city"] = "Austin",
            ["state"] = "TX",
            ["zip_code"] = "78701",
            ["contact_time_any"] = "true",
            ["contact_method_email"] = "true",
            ["consent_contact"] = "yes",
            ["language"] = "English",
            ["lead_source"] = "website",
            ["interest_level"] = "high",
            ["lead_intent"] = "buy",
            ["intent_protect_loved_ones"] = "true",
            ["intent_pay_mortgage"] = "false",
            ["intent_prepare_future"] = "false",
            ["intent_peace_of_mind"] = "false",
            ["intent_cover_expenses"] = "false",
            ["intent_unsure"] = "false",
            ["tobacco_use"] = "no",
            ["condition_none"] = "true",
            ["health_insurance"] = "yes",
            ["height"] = "5ft 7in",
            ["weight"] = "150",
            ["overall_health_status"] = "good",
            ["has_home_equity"] = "yes",
            ["home_equity_amount"] = "100000",
            ["savings_amount"] = "25000",
            ["investments_amount"] = "50000",
            ["retirement_amount"] = "75000",
            ["credit_card_debt"] = "0",
            ["student_loans"] = "0",
            ["auto_loans"] = "10000",
            ["mortgage_debt"] = "150000",
            ["other_debt"] = "0",
            ["coverage_type"] = "term_life",
            ["coverage_start_time"] = "asap",
            ["coverage_amount"] = "over_250k",
            ["monthly_budget"] = "100_200",
            ["marital_status"] = "Married",
            ["has_dependents"] = "yes",
            ["no_of_children"] = "1",
            ["ageRange_0_5"] = "true",
            ["employment_status"] = "Full-Time",
            ["household_income"] = "Over $100k",
            ["occupation"] = "Engineer",
            ["years_employed"] = "5_plus",
            ["beneficiary_name"] = "Grace Prospect",
            ["beneficiary_relation"] = "Spouse",
            ["beneficiary_dob"] = "1991-03-04",
            ["beneficiary_percentage"] = 100
        };

    private sealed class InsuranceRuntimeFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly bool _ownsProvider;
        private readonly AsyncServiceScope _scope;
        private readonly IConversationOutputSubscription _subscription;
        private readonly CancellationTokenSource _readCancellation = new(TimeSpan.FromSeconds(30));
        private readonly Channel<AdaptiveCardOutput> _cards = Channel.CreateUnbounded<AdaptiveCardOutput>();
        private readonly Task _reader;

        public InsuranceRuntimeFixture(ServiceProvider provider, bool ownsProvider = true)
        {
            _provider = provider;
            _ownsProvider = ownsProvider;
            _scope = provider.CreateAsyncScope();
            Runtime = _scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
            Tools = _scope.ServiceProvider.GetRequiredService<RecordingToolExecutor>();
            _subscription = Runtime.Subscribe();
            _reader = ReadOutputsAsync();
        }

        public IConversationRuntime Runtime { get; }
        public RecordingToolExecutor Tools { get; }
        public ConcurrentQueue<ConversationOutput> Outputs { get; } = new();

        public async Task RunFullQualificationAsync(
            IReadOnlyDictionary<string, object>? cardData = null)
        {
            await DriveQualificationCardsAsync(cardData: cardData);
            await WaitForOutputAsync("qualification_complete");
        }

        public async Task DriveQualificationCardsAsync(
            int cardCount = 9,
            IReadOnlyDictionary<string, object>? cardData = null)
        {
            await Runtime.StartAsync();
            cardData ??= ValidCardData;
            var submittedCards = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < cardCount; index++)
            {
                AdaptiveCardOutput card;
                try
                {
                    do
                    {
                        card = await _cards.Reader.ReadAsync(_readCancellation.Token)
                            .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    while (!submittedCards.Add(card.CardId));
                }
                catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
                {
                    var emittedCards = string.Join(", ", Outputs
                        .OfType<AdaptiveCardOutput>()
                        .Select(output => output.CardId));
                    var recentOutputs = string.Join(" | ", Outputs.TakeLast(12).Select(DescribeOutput));
                    throw new TimeoutException(
                        $"Timed out waiting for required card {index + 1}. " +
                        $"Emitted cards: [{emittedCards}]. Tools: [{string.Join(", ", Tools.ToolIds)}]. " +
                        $"Recent outputs: {recentOutputs}",
                        exception);
                }
                await Runtime.SubmitCardAsync(new CardSubmission(card.CardId, cardData));
            }
        }

        public async Task<AdaptiveCardOutput> ReadNextCardAsync() =>
            await _cards.Reader.ReadAsync(_readCancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        private async Task ReadOutputsAsync()
        {
            try
            {
                await foreach (var output in _subscription.ReadAllAsync(_readCancellation.Token))
                {
                    Outputs.Enqueue(output);
                    if (output is AdaptiveCardOutput card)
                        await _cards.Writer.WriteAsync(card, _readCancellation.Token);
                }
            }
            catch (OperationCanceledException) when (_readCancellation.IsCancellationRequested)
            {
            }
        }

        private async Task WaitForOutputAsync(string eventName)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < timeout)
            {
                if (Outputs.OfType<HostNotificationOutput>().Any(output => output.EventName == eventName))
                    return;
                await Task.Delay(10);
            }

            throw new TimeoutException($"Host notification '{eventName}' was not emitted.");
        }

        private static string DescribeOutput(ConversationOutput output) => output switch
        {
            MessageOutput message => $"message:{message.Message}",
            TopicLifecycleOutput topic => $"topic:{topic.State}:{topic.Detail}",
            ActivityLifecycleOutput activity =>
                $"activity:{activity.ActivityId}:{activity.State}:{activity.Detail}",
            AdaptiveCardOutput card => $"card:{card.CardId}:{card.RenderMode}",
            _ => output.GetType().Name
        };

        public async ValueTask DisposeAsync()
        {
            _readCancellation.Cancel();
            await _subscription.DisposeAsync();
            try { await _reader; } catch (OperationCanceledException) { }
            await _scope.DisposeAsync();
            if (_ownsProvider)
                await _provider.DisposeAsync();
            _readCancellation.Dispose();
        }
    }

    private sealed class CircuitLeadIdSource
    {
        private int _next = 100;
        public int Next() => Interlocked.Increment(ref _next);
    }

    private sealed class ConsentRuntimeFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;
        private readonly IConversationOutputSubscription _subscription;
        private readonly CancellationTokenSource _cancellation = new(TimeSpan.FromSeconds(15));
        private readonly Channel<AdaptiveCardOutput> _cards = Channel.CreateUnbounded<AdaptiveCardOutput>();
        private readonly ConcurrentDictionary<string, byte> _observedCardIds = new(StringComparer.Ordinal);
        private readonly Task _reader;

        public ConsentRuntimeFixture(ServiceProvider provider, BranchRecorder branches)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
            Runtime = _scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
            Branches = branches;
            _subscription = Runtime.Subscribe();
            _reader = ReadOutputsAsync();
        }

        public IConversationRuntime Runtime { get; }
        public BranchRecorder Branches { get; }

        public async Task<AdaptiveCardOutput> ReadCardAsync() =>
            await _cards.Reader.ReadAsync(_cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public void ResetObservedCards() => _observedCardIds.Clear();

        private async Task ReadOutputsAsync()
        {
            try
            {
                await foreach (var output in _subscription.ReadAllAsync(_cancellation.Token))
                    if (output is AdaptiveCardOutput card && _observedCardIds.TryAdd(card.CardId, 0))
                        await _cards.Writer.WriteAsync(card, _cancellation.Token);
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cancellation.Cancel();
            await _subscription.DisposeAsync();
            try { await _reader; } catch (OperationCanceledException) { }
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            _cancellation.Dispose();
        }
    }

    private sealed class MarketingT2RuntimeFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;
        private readonly IConversationOutputSubscription _subscription;
        private readonly CancellationTokenSource _cancellation = new(TimeSpan.FromSeconds(15));
        private readonly Channel<AdaptiveCardOutput> _cards = Channel.CreateUnbounded<AdaptiveCardOutput>();
        private readonly Task _reader;

        public MarketingT2RuntimeFixture(ServiceProvider provider)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
            Runtime = _scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
            _subscription = Runtime.Subscribe();
            _reader = ReadOutputsAsync();
        }

        public IConversationRuntime Runtime { get; }
        public ConcurrentQueue<ConversationOutput> Outputs { get; } = new();

        public async Task<AdaptiveCardOutput> ReadCardAsync() =>
            await _cards.Reader.ReadAsync(_cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public async Task WaitForOutputAsync(string eventName)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < timeout)
            {
                if (Outputs.OfType<HostNotificationOutput>().Any(output => output.EventName == eventName))
                    return;
                await Task.Delay(10);
            }

            throw new TimeoutException($"Host notification '{eventName}' was not emitted.");
        }

        private async Task ReadOutputsAsync()
        {
            try
            {
                await foreach (var output in _subscription.ReadAllAsync(_cancellation.Token))
                {
                    Outputs.Enqueue(output);
                    if (output is AdaptiveCardOutput card)
                        await _cards.Writer.WriteAsync(card, _cancellation.Token);
                }
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cancellation.Cancel();
            await _subscription.DisposeAsync();
            try { await _reader; } catch (OperationCanceledException) { }
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            _cancellation.Dispose();
        }
    }

    private sealed class BranchRecorder
    {
        public ConcurrentQueue<string> ActivatedTopicIds { get; } = new();
    }

    private sealed class BranchProbeTopic : TopicFlow
    {
        public BranchProbeTopic(
            TopicWorkflowContext context,
            ILogger<BranchProbeTopic> logger,
            BranchRecorder recorder,
            string topicId)
            : base(context, logger, topicId)
        {
            Add(SimpleActivity.Create("record-branch", _ => recorder.ActivatedTopicIds.Enqueue(topicId)));
        }
    }

    private sealed class FallbackRuntimeFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;

        public FallbackRuntimeFixture(ServiceProvider provider, FallbackProbeState probe)
        {
            _provider = provider;
            _scope = provider.CreateAsyncScope();
            Runtime = _scope.ServiceProvider.GetRequiredService<IConversationRuntime>();
            Probe = probe;
        }

        public IConversationRuntime Runtime { get; }
        public FallbackProbeState Probe { get; }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    private sealed class FallbackProbeState
    {
        public ConcurrentQueue<string> ActiveMessages { get; } = new();
        public ConcurrentQueue<string> FallbackMessages { get; } = new();
    }

    private sealed class DecliningInsuranceTopic(FallbackProbeState state) : ITopic
    {
        private readonly TopicWorkflowContext _context = new();
        private int _calls;

        public string Name => "insurance.e2e.active";
        public int Priority => 0;
        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(0f);

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            state.ActiveMessages.Enqueue(message);
            _calls++;
            if (_calls == 1 || message == "Interrupted topic completed")
                return Task.FromResult(TopicResult.CreateResponse("waiting", _context, requiresInput: true));

            return Task.FromResult(new TopicResult { IsHandled = false, wfContext = _context });
        }
    }

    private sealed class FallbackProbeTopic(FallbackProbeState state) : ITopic
    {
        private readonly TopicWorkflowContext _context = new();
        public string Name => "system.fallback";
        public int Priority => 0;
        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(0f);

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            state.FallbackMessages.Enqueue(message);
            return Task.FromResult(TopicResult.CreateCompleted("fallback answered", _context));
        }
    }

    private sealed class RecordingToolExecutor(string? failedToolId = null, int leadId = 42) : IToolExecutor
    {
        public int LeadId { get; } = leadId;
        public ConcurrentQueue<string> ToolIds { get; } = new();
        public ConcurrentQueue<object?> Requests { get; } = new();

        public ValueTask<ToolResult<TResult>> ExecuteAsync<TRequest, TResult>(
            string toolId,
            TRequest request,
            ToolExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ToolIds.Enqueue(toolId);
            Requests.Enqueue(request);

            if (string.Equals(toolId, failedToolId, StringComparison.OrdinalIgnoreCase))
                return ValueTask.FromResult(ToolResult<TResult>.Failure(
                    "e2e_injected_failure",
                    "Injected persistence failure."));

            object result = typeof(TResult) == typeof(CreateLeadResult)
                ? new CreateLeadResult(LeadId)
                : typeof(TResult) == typeof(ProfileWriteResult)
                    ? new ProfileWriteResult(LeadId, toolId)
                    : typeof(TResult) == typeof(QualifiedLeadHandoffResponse)
                        ? new QualifiedLeadHandoffResponse(LeadId, "Qualified", DateTimeOffset.UtcNow, false)
                        : throw new InvalidOperationException($"Unexpected tool result type {typeof(TResult).Name}.");

            return ValueTask.FromResult(ToolResult<TResult>.Success((TResult)result));
        }
    }

    private sealed class EmptyVectorDatabaseService : IVectorDatabaseService
    {
        public Task<bool> StoreDocumentAsync(string collectionName, string documentId, string content,
            Dictionary<string, object>? metadata = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<int> StoreBatchAsync(string collectionName, List<DocumentChunk> documentChunks,
            CancellationToken cancellationToken = default) => Task.FromResult(documentChunks.Count);

        public Task<List<DocumentSearchResult>> SearchAsync(string collectionName, string query, int limit = 5,
            double minRelevanceScore = 0.7, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<DocumentSearchResult>());

        public Task<List<string>> GetCollectionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<string>());

        public Task<bool> RemoveDocumentAsync(string collectionName, string documentId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> ClearCollectionAsync(string collectionName,
            CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<int> GetDocumentCountAsync(string collectionName,
            CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<List<DocumentSearchResult>> GetDocumentsByMetadataAsync(string collectionName,
            Dictionary<string, object> metadata, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<DocumentSearchResult>());

        public Task<float[]> GenerateEmbeddingAsync(string text,
            CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<float>());
    }
}
