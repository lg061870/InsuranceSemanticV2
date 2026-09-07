using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Models; // TopicResult
using ConversaCore.Registration;
using ConversaCore.Registration.Compatibility;
using ConversaCore.Topics; // ITopic
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConversaCore.Tests.Registration;

/// <summary>
/// CC-105 acceptance evidence (ConversaCore transformation work breakdown, WP1): "Provide
/// compatibility registration. Translate existing <c>IEnumerable&lt;ITopic&gt;</c>
/// registrations into descriptors during the migration window."
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-world registration style investigated.</b>
/// <c>InsuranceAgent.Extensions.InsuranceTopicRegistrationExtensions.AddInsuranceTopics</c>
/// and <c>InsuranceLeadsAgent.Configuration.ConversaCoreTopicRegistration.AddConversaCoreDomainTopics</c>
/// (the two real hosts in this repository) register every one of their topics exclusively
/// via <c>services.AddScoped&lt;ITopic&gt;(sp =&gt; new SomeTopic(...))</c> — i.e. 100%
/// <b>factory-based</b> <see cref="ServiceDescriptor"/> entries
/// (<see cref="ServiceDescriptor.ImplementationFactory"/> populated). Neither host uses
/// <c>services.AddScoped&lt;ITopic, ConcreteType&gt;()</c> (type-based,
/// <see cref="ServiceDescriptor.ImplementationType"/>) or a pre-built singleton instance
/// (<see cref="ServiceDescriptor.ImplementationInstance"/>) registration for topics. This
/// test class still covers all three <see cref="ServiceDescriptor"/> shapes plus keyed
/// services, because <see cref="ConversaCoreBuilderCompatibilityExtensions"/> is a general
/// compatibility capability, not a one-host special case — but the factory-based tests are
/// the ones that matter most against real evidence.
/// </para>
/// <para>
/// Uses local fake/probe <see cref="ITopic"/> implementations only (matching the pattern
/// used by <c>TopicRegistrationTests</c> and
/// <c>TopicRegistrationInstanceSeparationTests</c>), never real InsuranceAgent topics.
/// </para>
/// </remarks>
public class TopicCompatibilityRegistrationTests
{
    // ─────────────────────────────────────────────────────────────────
    // Probe ITopic implementations
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Probe for a type-based legacy registration (<c>services.AddScoped&lt;ITopic, T&gt;()</c>).
    /// Its constructor throws if actually invoked, proving the scan/translation never
    /// constructs it (CC-104's premature-instantiation guarantee must extend to CC-105).
    /// </summary>
    public sealed class ThrowsIfConstructed_TypeBasedProbe : ITopic
    {
        public ThrowsIfConstructed_TypeBasedProbe() => throw ConstructionAttempted(nameof(ThrowsIfConstructed_TypeBasedProbe));

        public string Name => nameof(ThrowsIfConstructed_TypeBasedProbe);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    /// <summary>
    /// Second type-based probe, used only by the "multiple legacy registrations coexist"
    /// test so the type-based and factory-based entries have distinct identities.
    /// </summary>
    public sealed class ThrowsIfConstructed_TypeBasedProbeTwo : ITopic
    {
        public ThrowsIfConstructed_TypeBasedProbeTwo() => throw ConstructionAttempted(nameof(ThrowsIfConstructed_TypeBasedProbeTwo));

        public string Name => nameof(ThrowsIfConstructed_TypeBasedProbeTwo);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    /// <summary>
    /// Probe for a factory-based legacy registration
    /// (<c>services.AddScoped&lt;ITopic&gt;(sp =&gt; new T(...))</c>) — the real pattern
    /// used by <c>AddInsuranceTopics</c>. Its constructor throws if actually invoked,
    /// proving the wrapped factory delegate is stored, never called, by the scan.
    /// </summary>
    public sealed class ThrowsIfConstructed_FactoryBasedProbe : ITopic
    {
        public ThrowsIfConstructed_FactoryBasedProbe() => throw ConstructionAttempted(nameof(ThrowsIfConstructed_FactoryBasedProbe));

        public string Name => nameof(ThrowsIfConstructed_FactoryBasedProbe);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    /// <summary>
    /// Probe used for the keyed-service skip test. It also throws if constructed, so the
    /// test proves the scan does not even attempt DI activation for a keyed entry it
    /// cannot safely inspect.
    /// </summary>
    public sealed class ThrowsIfConstructed_KeyedProbe : ITopic
    {
        public ThrowsIfConstructed_KeyedProbe() => throw ConstructionAttempted(nameof(ThrowsIfConstructed_KeyedProbe));

        public string Name => nameof(ThrowsIfConstructed_KeyedProbe);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    /// <summary>
    /// Probe for an instance-based legacy registration
    /// (<c>services.AddSingleton&lt;ITopic&gt;(instance)</c>). Unlike the other probes,
    /// this one must actually construct successfully in the test's Arrange step, since a
    /// pre-built instance has to exist before it can be registered as
    /// <see cref="ServiceDescriptor.ImplementationInstance"/> — reading its already-resolved
    /// <see cref="ITopic.Name"/> during the scan is safe (CC-105's task description: "already
    /// resolved and sitting in memory").
    /// </summary>
    public sealed class NamedProbeTopic : ITopic
    {
        public NamedProbeTopic(string name) => Name = name;

        public string Name { get; }
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    private static InvalidOperationException ConstructionAttempted(string probeTypeName) =>
        new($"{probeTypeName} was constructed. AddTopicsFromLegacyRegistrations must never " +
            "construct or invoke a legacy ITopic registration; it only wraps it.");

    // ─────────────────────────────────────────────────────────────────
    // Test helpers
    // ─────────────────────────────────────────────────────────────────

    private static ConversaCoreBuilder NewBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return new ConversaCoreBuilder(services);
    }

    private static List<TopicDescriptor> ResolveDescriptors(ConversaCoreBuilder builder)
    {
        using var provider = builder.Services.BuildServiceProvider();
        return provider.GetServices<TopicDescriptor>().ToList();
    }

    // ─────────────────────────────────────────────────────────────────
    // Type-based legacy registration
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromLegacyRegistrations_TypeBasedRegistration_TranslatesWithoutConstructingTheTopic()
    {
        var builder = NewBuilder();
        builder.Services.AddScoped<ITopic, ThrowsIfConstructed_TypeBasedProbe>();

        Action act = () => builder.AddTopicsFromLegacyRegistrations();

        act.Should().NotThrow();

        var descriptors = ResolveDescriptors(builder);
        descriptors.Should().ContainSingle()
            .Which.TopicId.Should().Be(typeof(ThrowsIfConstructed_TypeBasedProbe).FullName);
    }

    [Fact]
    public void AddTopicsFromLegacyRegistrations_TypeBasedRegistration_ReturnsTheBuilderForChaining()
    {
        var builder = NewBuilder();
        builder.Services.AddScoped<ITopic, ThrowsIfConstructed_TypeBasedProbe>();

        var result = builder.AddTopicsFromLegacyRegistrations();

        result.Should().BeSameAs(builder);
    }

    // ─────────────────────────────────────────────────────────────────
    // Factory-based legacy registration (the real InsuranceAgent/InsuranceLeadsAgent pattern)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromLegacyRegistrations_FactoryBasedRegistration_TranslatesWithoutInvokingTheFactory()
    {
        var builder = NewBuilder();
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe());

        Action act = () => builder.AddTopicsFromLegacyRegistrations();

        act.Should().NotThrow();

        var descriptors = ResolveDescriptors(builder);
        descriptors.Should().ContainSingle()
            .Which.TopicId.Should().Be("legacy-topic-0");
    }

    [Fact]
    public void AddTopicsFromLegacyRegistrations_FactoryBasedRegistration_WrappedFactoryStillConstructsWhenExplicitlyInvoked()
    {
        // Sanity check (mirrors CC-104's own sanity check): prove the "NotThrow" assertion
        // above is meaningful because the wrapped factory does construct (and this probe
        // throws) when a caller later, deliberately, invokes TopicDescriptor.Factory.
        var builder = NewBuilder();
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe());
        builder.AddTopicsFromLegacyRegistrations();

        var descriptor = ResolveDescriptors(builder).Single();
        using var provider = builder.Services.BuildServiceProvider();

        Action act = () => descriptor.Factory(provider);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(ThrowsIfConstructed_FactoryBasedProbe)} was constructed*");
    }

    // ─────────────────────────────────────────────────────────────────
    // Instance-based legacy registration
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromLegacyRegistrations_InstanceBasedRegistration_UsesTheInstancesRealName()
    {
        var builder = NewBuilder();
        var instance = new NamedProbeTopic("already-resolved-topic");
        builder.Services.AddSingleton<ITopic>(instance);

        builder.AddTopicsFromLegacyRegistrations();

        var descriptors = ResolveDescriptors(builder);
        descriptors.Should().ContainSingle().Which.TopicId.Should().Be("already-resolved-topic");
    }

    [Fact]
    public void AddTopicsFromLegacyRegistrations_InstanceBasedRegistration_FactoryReturnsTheSameInstance()
    {
        var builder = NewBuilder();
        var instance = new NamedProbeTopic("already-resolved-topic");
        builder.Services.AddSingleton<ITopic>(instance);
        builder.AddTopicsFromLegacyRegistrations();

        var descriptor = ResolveDescriptors(builder).Single();
        using var provider = builder.Services.BuildServiceProvider();

        var resolvedTopic = descriptor.Factory(provider);

        resolvedTopic.Should().BeSameAs(instance);
    }

    // ─────────────────────────────────────────────────────────────────
    // Multiple legacy registrations coexisting
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromLegacyRegistrations_MultipleLegacyRegistrations_AllAreTranslated()
    {
        var builder = NewBuilder();
        var instance = new NamedProbeTopic("already-resolved-topic");

        // Interleaved on purpose: type-based, factory-based, instance-based, factory-based,
        // type-based -- proving the scan handles a realistic mixed batch, not just one shape
        // at a time, and that the positional factory index only counts factory entries.
        builder.Services.AddScoped<ITopic, ThrowsIfConstructed_TypeBasedProbe>();
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe());
        builder.Services.AddSingleton<ITopic>(instance);
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe());
        builder.Services.AddScoped<ITopic, ThrowsIfConstructed_TypeBasedProbeTwo>();

        builder.AddTopicsFromLegacyRegistrations();

        var topicIds = ResolveDescriptors(builder).Select(d => d.TopicId).ToList();
        topicIds.Should().HaveCount(5);
        topicIds.Should().Contain(new[]
        {
            typeof(ThrowsIfConstructed_TypeBasedProbe).FullName!,
            typeof(ThrowsIfConstructed_TypeBasedProbeTwo).FullName!,
            "already-resolved-topic",
            "legacy-topic-0",
            "legacy-topic-1"
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Caller-supplied factory IDs: positional, matched by factory-encounter order
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromLegacyRegistrations_CallerSuppliedFactoryTopicIds_OverrideThePositionalDefaults()
    {
        var builder = NewBuilder();
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe());
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe());

        builder.AddTopicsFromLegacyRegistrations(factoryTopicIds: new[] { "conversation-start", "compliance" });

        var topicIds = ResolveDescriptors(builder).Select(d => d.TopicId).ToList();
        topicIds.Should().BeEquivalentTo(new[] { "conversation-start", "compliance" });
    }

    [Fact]
    public void AddTopicsFromLegacyRegistrations_FewerCallerSuppliedIdsThanFactories_RemainingFactoriesKeepTheirOwnPositionalDefault()
    {
        // Documents the exact matching rule: factoryTopicIds[i] overrides only the i-th
        // factory-encounter index. A factory beyond the supplied list's length falls back
        // to "legacy-topic-{its own factory-encounter index}" -- not renumbered as if the
        // override list had never been supplied.
        var builder = NewBuilder();
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe()); // index 0
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe()); // index 1
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe()); // index 2

        builder.AddTopicsFromLegacyRegistrations(factoryTopicIds: new[] { "conversation-start" });

        var topicIds = ResolveDescriptors(builder).Select(d => d.TopicId).ToList();
        topicIds.Should().BeEquivalentTo(new[] { "conversation-start", "legacy-topic-1", "legacy-topic-2" });
    }

    // ─────────────────────────────────────────────────────────────────
    // Interaction with CC-102's duplicate-ID check and CC-103's validation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromLegacyRegistrations_DuplicateIdAgainstAnExistingDescriptor_ThrowsViaCC102Check()
    {
        var builder = NewBuilder();
        var collidingId = typeof(ThrowsIfConstructed_TypeBasedProbe).FullName!;
        builder.AddTopic(new TopicDescriptor(collidingId, _ => new ThrowsIfConstructed_TypeBasedProbe()));
        builder.Services.AddScoped<ITopic, ThrowsIfConstructed_TypeBasedProbe>();

        Action act = () => builder.AddTopicsFromLegacyRegistrations();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{collidingId}*already registered*");
    }

    [Fact]
    public void AddTopicsFromLegacyRegistrations_ResultingDescriptors_PassCC103StartAndFallbackValidation()
    {
        var builder = NewBuilder();
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe()); // -> "start"
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe()); // -> "fallback"

        builder.AddTopicsFromLegacyRegistrations(factoryTopicIds: new[] { "start", "fallback" });

        var result = builder.ValidateTopicRegistrations(startTopicId: "start", fallbackTopicId: "fallback");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AddTopicsFromLegacyRegistrations_MissingDesignatedStartTopic_FailsCC103Validation()
    {
        var builder = NewBuilder();
        builder.Services.AddScoped<ITopic>(sp => new ThrowsIfConstructed_FactoryBasedProbe());
        builder.AddTopicsFromLegacyRegistrations(); // -> "legacy-topic-0", not "start"

        var result = builder.ValidateTopicRegistrations(startTopicId: "start");

        result.IsValid.Should().BeFalse();
    }

    // ─────────────────────────────────────────────────────────────────
    // Keyed ITopic registrations: skipped gracefully, not crashed on, not silently lost
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromLegacyRegistrations_KeyedServiceRegistration_IsSkippedWithoutThrowingOrConstructing()
    {
        var builder = NewBuilder();
        builder.Services.AddKeyedScoped<ITopic, ThrowsIfConstructed_KeyedProbe>("some-key");

        Action act = () => builder.AddTopicsFromLegacyRegistrations();

        act.Should().NotThrow();
        ResolveDescriptors(builder).Should().BeEmpty();
    }

    [Fact]
    public void AddTopicsFromLegacyRegistrations_KeyedServiceRegistration_RecordsAClearSkipReason()
    {
        var builder = NewBuilder();
        builder.Services.AddKeyedScoped<ITopic, ThrowsIfConstructed_KeyedProbe>("some-key");
        var skipped = new List<string>();

        builder.AddTopicsFromLegacyRegistrations(skippedRegistrations: skipped);

        skipped.Should().ContainSingle(reason =>
            reason.Contains("keyed", StringComparison.OrdinalIgnoreCase) &&
            reason.Contains("some-key"));
    }

    // ─────────────────────────────────────────────────────────────────
    // No legacy registrations present: a no-op, not an error
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromLegacyRegistrations_NoLegacyRegistrationsPresent_IsANoOp()
    {
        var builder = NewBuilder();

        Action act = () => builder.AddTopicsFromLegacyRegistrations();

        act.Should().NotThrow();
        ResolveDescriptors(builder).Should().BeEmpty();
    }

    [Fact]
    public void AddTopicsFromLegacyRegistrations_NullBuilder_ThrowsArgumentNullException()
    {
        ConversaCoreBuilder builder = null!;

        Action act = () => builder.AddTopicsFromLegacyRegistrations();

        act.Should().Throw<ArgumentNullException>();
    }
}
