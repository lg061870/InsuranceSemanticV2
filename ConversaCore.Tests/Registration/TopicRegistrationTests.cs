using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Models; // TopicResult
using ConversaCore.Registration;
using ConversaCore.Topics; // ITopic
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConversaCore.Tests.Registration;

/// <summary>
/// Coverage for CC-102: the <see cref="ConversaCoreBuilder"/> topic-registration
/// extension methods in <see cref="ConversaCoreBuilderTopicExtensions"/> —
/// <c>AddTopic&lt;TTopic&gt;()</c> via DI activation, <c>AddTopic&lt;TTopic&gt;()</c> via
/// an explicit factory, direct <see cref="TopicDescriptor"/> registration, duplicate-ID
/// rejection, and <c>AddTopicsFromAssemblyContaining&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// Uses local fake/probe <see cref="ITopic"/> implementations only (matching the pattern
/// used by <c>TopicDescriptorTests.FakeTopic</c> and
/// <c>LegacyOrchestrationTests.RoutingProbe</c>), never real InsuranceAgent topics.
/// </remarks>
public class TopicRegistrationTests
{
    // ─────────────────────────────────────────────────────────────────
    // Probe ITopic implementations
    // ─────────────────────────────────────────────────────────────────

    /// <summary>A dependency resolvable via ordinary DI, used to prove constructor injection actually ran.</summary>
    public sealed class ProbeDependency
    {
        public string Label { get; }
        public ProbeDependency(string label) => Label = label;
    }

    /// <summary>An <see cref="ITopic"/> with a public parameterless constructor: the simplest DI-activation case.</summary>
    public sealed class ProbeTopicWithDefaultConstructor : ITopic
    {
        public string Name => "ProbeTopicWithDefaultConstructor";
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by registration tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by registration tests.");
    }

    /// <summary>An <see cref="ITopic"/> whose constructor takes a DI-resolvable dependency, proving DI activation resolves constructor arguments from the service provider.</summary>
    public sealed class ProbeTopicWithInjectedDependency : ITopic
    {
        public ProbeDependency Dependency { get; }
        public ProbeTopicWithInjectedDependency(ProbeDependency dependency) => Dependency = dependency;

        public string Name => "ProbeTopicWithInjectedDependency";
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by registration tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by registration tests.");
    }

    /// <summary>An <see cref="ITopic"/> whose constructor takes a plain value DI cannot resolve, requiring an explicit factory.</summary>
    public sealed class ProbeTopicRequiringManualConstruction : ITopic
    {
        public string Label { get; }
        public ProbeTopicRequiringManualConstruction(string label) => Label = label;

        public string Name => "ProbeTopicRequiringManualConstruction";
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by registration tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by registration tests.");
    }

    /// <summary>A second simple probe type used to exercise <c>AddTopicsFromAssemblyContaining&lt;T&gt;()</c>.</summary>
    public sealed class ScanProbeTopicA : ITopic
    {
        public string Name => "ScanProbeTopicA";
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by registration tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by registration tests.");
    }

    /// <summary>A third simple probe type used to exercise <c>AddTopicsFromAssemblyContaining&lt;T&gt;()</c>.</summary>
    public sealed class ScanProbeTopicB : ITopic
    {
        public string Name => "ScanProbeTopicB";
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by registration tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by registration tests.");
    }

    /// <summary>An abstract <see cref="ITopic"/> that must never be picked up by the assembly scan (not concrete).</summary>
    public abstract class AbstractProbeTopic : ITopic
    {
        public string Name => "AbstractProbeTopic";
        public int Priority => 0;
        public abstract Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default);
        public abstract Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default);
    }

    // ─────────────────────────────────────────────────────────────────
    // Test helpers
    // ─────────────────────────────────────────────────────────────────

    private static ConversaCoreBuilder NewBuilder(Action<ServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configureServices?.Invoke(services);
        return new ConversaCoreBuilder(services);
    }

    private static IReadOnlyList<TopicDescriptor> ResolveDescriptors(ConversaCoreBuilder builder)
    {
        using var provider = builder.Services.BuildServiceProvider();
        return provider.GetServices<TopicDescriptor>().ToList();
    }

    // ─────────────────────────────────────────────────────────────────
    // AddTopic<T>() — DI activation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopic_WithDefaultConstructorViaDiActivation_RegistersDescriptorResolvableFromServices()
    {
        var builder = NewBuilder();

        builder.AddTopic<ProbeTopicWithDefaultConstructor>("probe.default-ctor");

        var descriptors = ResolveDescriptors(builder);
        descriptors.Should().ContainSingle(d => d.TopicId == "probe.default-ctor");
    }

    [Fact]
    public void AddTopic_WithDiActivation_FactoryResolvesConstructorDependenciesFromServiceProvider()
    {
        var builder = NewBuilder(services => services.AddSingleton(new ProbeDependency("injected-label")));
        builder.AddTopic<ProbeTopicWithInjectedDependency>("probe.with-dependency");

        using var provider = builder.Services.BuildServiceProvider();
        var descriptor = provider.GetServices<TopicDescriptor>().Single(d => d.TopicId == "probe.with-dependency");

        var topic = descriptor.Factory(provider);

        topic.Should().BeOfType<ProbeTopicWithInjectedDependency>();
        ((ProbeTopicWithInjectedDependency)topic).Dependency.Label.Should().Be("injected-label");
    }

    [Fact]
    public void AddTopic_DiActivation_ReturnsSameBuilderForChaining()
    {
        var builder = NewBuilder();

        var result = builder.AddTopic<ProbeTopicWithDefaultConstructor>("probe.default-ctor");

        result.Should().BeSameAs(builder);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddTopic_DiActivation_WithInvalidTopicId_ThrowsArgumentException(string? invalidTopicId)
    {
        var builder = NewBuilder();

        Action act = () => builder.AddTopic<ProbeTopicWithDefaultConstructor>(invalidTopicId!);

        act.Should().Throw<ArgumentException>().WithParameterName("topicId");
    }

    // ─────────────────────────────────────────────────────────────────
    // AddTopic<T>() — explicit factory
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopic_WithExplicitFactory_RegistersDescriptorUsingSuppliedFactory()
    {
        var builder = NewBuilder();

        builder.AddTopic<ProbeTopicRequiringManualConstruction>(
            "probe.manual",
            sp => new ProbeTopicRequiringManualConstruction("manual-label"));

        using var provider = builder.Services.BuildServiceProvider();
        var descriptor = provider.GetServices<TopicDescriptor>().Single(d => d.TopicId == "probe.manual");

        var topic = descriptor.Factory(provider);

        topic.Should().BeOfType<ProbeTopicRequiringManualConstruction>();
        ((ProbeTopicRequiringManualConstruction)topic).Label.Should().Be("manual-label");
    }

    [Fact]
    public void AddTopic_ExplicitFactory_WithNullFactory_ThrowsArgumentNullException()
    {
        var builder = NewBuilder();

        Action act = () => builder.AddTopic<ProbeTopicRequiringManualConstruction>(
            "probe.manual",
            (Func<IServiceProvider, ProbeTopicRequiringManualConstruction>)null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("factory");
    }

    [Fact]
    public void AddTopic_ExplicitFactory_ReturnsSameBuilderForChaining()
    {
        var builder = NewBuilder();

        var result = builder.AddTopic<ProbeTopicRequiringManualConstruction>(
            "probe.manual",
            sp => new ProbeTopicRequiringManualConstruction("x"));

        result.Should().BeSameAs(builder);
    }

    // ─────────────────────────────────────────────────────────────────
    // AddTopic(TopicDescriptor) — direct registration
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopic_WithPrebuiltDescriptor_RegistersItDirectly()
    {
        var builder = NewBuilder();
        var descriptor = new TopicDescriptor("probe.direct", _ => new ProbeTopicWithDefaultConstructor());

        builder.AddTopic(descriptor);

        var descriptors = ResolveDescriptors(builder);
        descriptors.Should().ContainSingle(d => d.TopicId == "probe.direct");
    }

    // ─────────────────────────────────────────────────────────────────
    // Descriptor metadata configuration
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopic_WithConfigureCallback_AppliesSuppliedMetadata()
    {
        var builder = NewBuilder();

        builder.AddTopic<ProbeTopicWithDefaultConstructor>("probe.with-metadata", options =>
        {
            options.DisplayName = "Friendly Name";
            options.Description = "Does probe things.";
            options.Priority = 7;
            options.Classification = TopicClassification.System;
            options.InterruptionPolicy = TopicInterruptionPolicy.Interruptible;
            options.AllowedToolIds = new HashSet<string> { "tool.a", "tool.b" };
        });

        var descriptor = ResolveDescriptors(builder).Single(d => d.TopicId == "probe.with-metadata");

        descriptor.DisplayName.Should().Be("Friendly Name");
        descriptor.Description.Should().Be("Does probe things.");
        descriptor.Priority.Should().Be(7);
        descriptor.Classification.Should().Be(TopicClassification.System);
        descriptor.InterruptionPolicy.Should().Be(TopicInterruptionPolicy.Interruptible);
        descriptor.AllowedToolIds.Should().BeEquivalentTo(new[] { "tool.a", "tool.b" });
    }

    [Fact]
    public void AddTopic_WithoutConfigureCallback_AppliesDocumentedDefaults()
    {
        var builder = NewBuilder();

        builder.AddTopic<ProbeTopicWithDefaultConstructor>("probe.defaults");

        var descriptor = ResolveDescriptors(builder).Single(d => d.TopicId == "probe.defaults");

        descriptor.DisplayName.Should().Be("probe.defaults");
        descriptor.Description.Should().BeEmpty();
        descriptor.Priority.Should().Be(0);
        descriptor.Classification.Should().Be(TopicClassification.Domain);
        descriptor.InterruptionPolicy.Should().Be(TopicInterruptionPolicy.FirstRefusal);
        descriptor.AllowedToolIds.Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────
    // Duplicate-ID rejection
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopic_WithDuplicateTopicId_ThrowsInvalidOperationException()
    {
        var builder = NewBuilder();
        builder.AddTopic<ProbeTopicWithDefaultConstructor>("dup.topic");

        Action act = () => builder.AddTopic<ProbeTopicWithInjectedDependency>("dup.topic");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*dup.topic*");
    }

    [Fact]
    public void AddTopic_WithDuplicateTopicIdDifferentCasing_ThrowsInvalidOperationException()
    {
        var builder = NewBuilder();
        builder.AddTopic<ProbeTopicWithDefaultConstructor>("Dup.Topic");

        Action act = () => builder.AddTopic<ProbeTopicWithDefaultConstructor>("dup.topic");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddTopic_WithDuplicateTopicId_DoesNotRegisterTheSecondDescriptor()
    {
        var builder = NewBuilder();
        builder.AddTopic<ProbeTopicWithDefaultConstructor>("dup.topic", options => options.DisplayName = "First");

        try
        {
            builder.AddTopic<ProbeTopicWithInjectedDependency>("dup.topic", options => options.DisplayName = "Second");
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        var descriptors = ResolveDescriptors(builder).Where(d => d.TopicId == "dup.topic").ToList();
        descriptors.Should().ContainSingle();
        descriptors.Single().DisplayName.Should().Be("First");
    }

    [Fact]
    public void AddTopic_WithPrebuiltDuplicateDescriptor_ThrowsInvalidOperationException()
    {
        var builder = NewBuilder();
        builder.AddTopic(new TopicDescriptor("dup.direct", _ => new ProbeTopicWithDefaultConstructor()));

        Action act = () => builder.AddTopic(new TopicDescriptor("dup.direct", _ => new ProbeTopicWithDefaultConstructor()));

        act.Should().Throw<InvalidOperationException>();
    }

    // ─────────────────────────────────────────────────────────────────
    // AddTopicsFromAssemblyContaining<T>()
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromAssemblyContaining_DiscoversConcreteProbeTopicsInThisAssembly()
    {
        var builder = NewBuilder();

        builder.AddTopicsFromAssemblyContaining<TopicRegistrationTests>();

        var descriptors = ResolveDescriptors(builder);
        descriptors.Should().Contain(d => d.TopicId == typeof(ScanProbeTopicA).FullName);
        descriptors.Should().Contain(d => d.TopicId == typeof(ScanProbeTopicB).FullName);
    }

    [Fact]
    public void AddTopicsFromAssemblyContaining_SkipsAbstractTypes()
    {
        var builder = NewBuilder();

        builder.AddTopicsFromAssemblyContaining<TopicRegistrationTests>();

        var descriptors = ResolveDescriptors(builder);
        descriptors.Should().NotContain(d => d.TopicId == typeof(AbstractProbeTopic).FullName);
    }

    [Fact]
    public void AddTopicsFromAssemblyContaining_ScannedDescriptorFactory_ConstructsTheExpectedConcreteType()
    {
        var builder = NewBuilder();
        builder.AddTopicsFromAssemblyContaining<TopicRegistrationTests>();

        using var provider = builder.Services.BuildServiceProvider();
        var descriptor = provider.GetServices<TopicDescriptor>()
            .Single(d => d.TopicId == typeof(ScanProbeTopicA).FullName);

        var topic = descriptor.Factory(provider);

        topic.Should().BeOfType<ScanProbeTopicA>();
    }

    [Fact]
    public void AddTopicsFromAssemblyContaining_ReturnsSameBuilderForChaining()
    {
        var builder = NewBuilder();

        var result = builder.AddTopicsFromAssemblyContaining<TopicRegistrationTests>();

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddTopicsFromAssemblyContaining_WhenScannedIdCollidesWithExistingRegistration_ThrowsInvalidOperationException()
    {
        var builder = NewBuilder();
        // Pre-claim the default ID the scan would assign to ScanProbeTopicA.
        builder.AddTopic(new TopicDescriptor(
            typeof(ScanProbeTopicA).FullName!,
            _ => new ProbeTopicWithDefaultConstructor()));

        Action act = () => builder.AddTopicsFromAssemblyContaining<TopicRegistrationTests>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{typeof(ScanProbeTopicA).FullName}*");
    }

    // ─────────────────────────────────────────────────────────────────
    // Fluent chaining across mixed registration calls
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RegistrationMethods_ChainTogetherAndAccumulateAllDescriptors()
    {
        var builder = NewBuilder(services => services.AddSingleton(new ProbeDependency("chained")));

        builder
            .AddTopic<ProbeTopicWithDefaultConstructor>("chain.default-ctor")
            .AddTopic<ProbeTopicWithInjectedDependency>("chain.with-dependency")
            .AddTopic<ProbeTopicRequiringManualConstruction>("chain.manual", sp => new ProbeTopicRequiringManualConstruction("m"))
            .AddTopic(new TopicDescriptor("chain.direct", _ => new ProbeTopicWithDefaultConstructor()));

        var topicIds = ResolveDescriptors(builder).Select(d => d.TopicId).ToList();
        topicIds.Should().Contain(new[]
        {
            "chain.default-ctor",
            "chain.with-dependency",
            "chain.manual",
            "chain.direct"
        });
    }
}
