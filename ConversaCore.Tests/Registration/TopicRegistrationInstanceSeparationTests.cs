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
/// CC-104 acceptance evidence (ConversaCore transformation work breakdown, WP1): "Separate
/// definitions from instances. Ensure registration builds immutable descriptors and
/// factories without resolving scoped topic objects."
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists.</b> CC-100 through CC-103 each already carry their own
/// single-path tests proving individual pieces never invoke <see cref="TopicDescriptor.Factory"/>
/// (for example, <c>TopicRegistrationValidationTests.ValidateTopicRegistrations_DoesNotInvokeAnyTopicDescriptorFactory</c>).
/// CC-104 is a separate, holistic ticket-level deliverable: one place that proves the
/// guarantee end-to-end, across every registration path CC-102 exposes, through CC-103's
/// aggregated validation, and through resolving the final <c>IEnumerable&lt;TopicDescriptor&gt;</c>
/// set a consumer would use — with nothing in between ever constructing a real
/// <see cref="ITopic"/> instance.
/// </para>
/// <para>
/// <b>Investigation result (no production gap found).</b> Re-reading
/// <see cref="ConversaCoreBuilderTopicExtensions"/>, <see cref="TopicRegistrationValidator"/>,
/// and <see cref="ConversaCoreBuilderValidationExtensions"/> confirms the guarantee already
/// holds by construction:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <c>AddTopic&lt;TTopic&gt;(topicId, configure)</c> stores
/// <c>sp =&gt; ActivatorUtilities.CreateInstance&lt;TTopic&gt;(sp)</c> as a delegate inside
/// <see cref="TopicDescriptor.Factory"/>; it is never invoked by the registration call.
/// </description></item>
/// <item><description>
/// <c>AddTopic&lt;TTopic&gt;(topicId, factory, configure)</c> wraps the caller's own
/// factory the same way — stored, not called.
/// </description></item>
/// <item><description>
/// <c>AddTopic(TopicDescriptor)</c> only reads <see cref="TopicDescriptor.TopicId"/> (via
/// <see cref="TopicDescriptor.Equals(TopicDescriptor?)"/>) to reject duplicates, then adds
/// the already-built descriptor as a singleton *instance* registration
/// (<c>services.AddSingleton(descriptor)</c>) — never touching <c>Factory</c>.
/// </description></item>
/// <item><description>
/// <c>AddTopicsFromAssemblyContaining&lt;T&gt;()</c> filters candidate types using only
/// <see cref="Type"/> reflection metadata (<c>IsClass</c>, <c>IsAbstract</c>,
/// <c>IsGenericTypeDefinition</c>, <c>ITopic.IsAssignableFrom</c>,
/// <c>GetConstructors(...).Length</c>) — none of which constructs an instance — then
/// builds the same kind of uninvoked factory delegate as the single-type DI-activation
/// overload.
/// </description></item>
/// <item><description>
/// <see cref="TopicRegistrationValidator.Validate"/> reads only
/// <see cref="TopicDescriptor.TopicId"/> strings (grouping for duplicates, comparing
/// against designated start/fallback IDs) and never references <c>.Factory</c> at all.
/// </description></item>
/// <item><description>
/// <see cref="ConversaCoreBuilderValidationExtensions.ValidateTopicRegistrations(ConversaCoreBuilder, string?, string?)"/>
/// resolves descriptors via <c>IServiceProvider.GetServices&lt;TopicDescriptor&gt;()</c>.
/// Because descriptors were registered as already-constructed singleton *instances*
/// (not singleton factories), the DI container returns the stored instance directly —
/// resolution performs no activation of anything, let alone a topic.
/// </description></item>
/// </list>
/// <para>
/// No production code changes were needed for CC-104. This file is the explicit,
/// consolidated acceptance evidence a reviewer (or CC-202, which later builds the real
/// runtime <c>ITopicCatalog</c>/<c>ITopicActivator</c> on top of this guarantee) can point
/// to.
/// </para>
/// <para>
/// Uses local fake/probe <see cref="ITopic"/> implementations only (matching the pattern
/// used by <c>TopicDescriptorTests.FakeTopic</c>, <c>TopicRegistrationTests</c>'s probes,
/// and <c>TopicRegistrationValidationTests.ProbeTopic</c>), never real InsuranceAgent
/// topics.
/// </para>
/// </remarks>
public class TopicRegistrationInstanceSeparationTests
{
    // ─────────────────────────────────────────────────────────────────
    // Probe ITopic implementations whose constructors throw if actually invoked. This is
    // the strictest possible proof: rather than only checking a counter stayed at zero
    // *after the fact*, an accidental construction anywhere in the pipeline fails the test
    // immediately, at the exact moment it would have happened.
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Probe for the <c>AddTopic&lt;TTopic&gt;(topicId, configure)</c> DI-activation path.
    /// A public parameterless constructor is required so <c>ActivatorUtilities.CreateInstance</c>
    /// *could* build it if the factory were ever (wrongly) invoked.
    /// </summary>
    public sealed class ThrowsIfConstructed_DiActivationProbe : ITopic
    {
        public ThrowsIfConstructed_DiActivationProbe() => throw ConstructionAttempted(nameof(ThrowsIfConstructed_DiActivationProbe));

        public string Name => nameof(ThrowsIfConstructed_DiActivationProbe);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    /// <summary>Probe for the explicit-<c>factory</c> <c>AddTopic&lt;TTopic&gt;</c> overload.</summary>
    public sealed class ThrowsIfConstructed_ExplicitFactoryProbe : ITopic
    {
        public ThrowsIfConstructed_ExplicitFactoryProbe() => throw ConstructionAttempted(nameof(ThrowsIfConstructed_ExplicitFactoryProbe));

        public string Name => nameof(ThrowsIfConstructed_ExplicitFactoryProbe);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    /// <summary>Probe for the direct <c>AddTopic(TopicDescriptor)</c> registration path.</summary>
    public sealed class ThrowsIfConstructed_DirectDescriptorProbe : ITopic
    {
        public ThrowsIfConstructed_DirectDescriptorProbe() => throw ConstructionAttempted(nameof(ThrowsIfConstructed_DirectDescriptorProbe));

        public string Name => nameof(ThrowsIfConstructed_DirectDescriptorProbe);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    /// <summary>
    /// First of two probes for <c>AddTopicsFromAssemblyContaining&lt;T&gt;()</c>. Two
    /// distinct types are used so the scan discovers more than one candidate, matching how
    /// <c>TopicRegistrationTests</c>'s own <c>ScanProbeTopicA</c>/<c>ScanProbeTopicB</c>
    /// pair exercises the scan.
    /// </summary>
    public sealed class ThrowsIfConstructed_ScanProbeA : ITopic
    {
        public ThrowsIfConstructed_ScanProbeA() => throw ConstructionAttempted(nameof(ThrowsIfConstructed_ScanProbeA));

        public string Name => nameof(ThrowsIfConstructed_ScanProbeA);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    /// <summary>Second scan probe; see <see cref="ThrowsIfConstructed_ScanProbeA"/>.</summary>
    public sealed class ThrowsIfConstructed_ScanProbeB : ITopic
    {
        public ThrowsIfConstructed_ScanProbeB() => throw ConstructionAttempted(nameof(ThrowsIfConstructed_ScanProbeB));

        public string Name => nameof(ThrowsIfConstructed_ScanProbeB);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    private static InvalidOperationException ConstructionAttempted(string probeTypeName) =>
        new($"{probeTypeName} was constructed. Registration, validation, and descriptor " +
            "resolution must never invoke TopicDescriptor.Factory; only an explicit, " +
            "caller-initiated Factory(...) call may construct a topic instance.");

    // ─────────────────────────────────────────────────────────────────
    // Test helpers
    // ─────────────────────────────────────────────────────────────────

    private static ConversaCoreBuilder NewBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return new ConversaCoreBuilder(services);
    }

    // ─────────────────────────────────────────────────────────────────
    // Each registration path, in isolation: none constructs its probe.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopic_DiActivation_DoesNotConstructTheTopicInstance()
    {
        var builder = NewBuilder();

        Action act = () => builder.AddTopic<ThrowsIfConstructed_DiActivationProbe>("cc104.di-activation");

        act.Should().NotThrow();
    }

    [Fact]
    public void AddTopic_ExplicitFactory_DoesNotInvokeTheSuppliedFactory()
    {
        var builder = NewBuilder();

        Action act = () => builder.AddTopic<ThrowsIfConstructed_ExplicitFactoryProbe>(
            "cc104.explicit-factory",
            sp => new ThrowsIfConstructed_ExplicitFactoryProbe());

        act.Should().NotThrow();
    }

    [Fact]
    public void AddTopic_DirectDescriptor_DoesNotInvokeTheDescriptorFactory()
    {
        var builder = NewBuilder();
        var descriptor = new TopicDescriptor(
            "cc104.direct-descriptor",
            _ => new ThrowsIfConstructed_DirectDescriptorProbe());

        Action act = () => builder.AddTopic(descriptor);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddTopicsFromAssemblyContaining_DoesNotConstructAnyDiscoveredTopicInstance()
    {
        var builder = NewBuilder();

        // The scan itself must not construct ThrowsIfConstructed_ScanProbeA/B, and it must
        // not construct any other concrete ITopic elsewhere in this test assembly either
        // (for example TopicRegistrationTests's own probes) -- the whole point of CC-104
        // is that scanning for registration candidates is metadata-only.
        Action act = () => builder.AddTopicsFromAssemblyContaining<TopicRegistrationInstanceSeparationTests>();

        act.Should().NotThrow();
    }

    // ─────────────────────────────────────────────────────────────────
    // Holistic pipeline: register through every path, validate, then resolve the final
    // descriptor set -- proving the guarantee holds end-to-end, not just per path.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FullPipeline_RegisterThroughEveryPath_Validate_AndResolveDescriptors_NeverConstructsAnyTopicInstance()
    {
        var builder = NewBuilder();

        // 1) AddTopic<TTopic>() via DI activation.
        builder.AddTopic<ThrowsIfConstructed_DiActivationProbe>("cc104.pipeline.di-activation");

        // 2) AddTopic<TTopic>() via an explicit factory.
        builder.AddTopic<ThrowsIfConstructed_ExplicitFactoryProbe>(
            "cc104.pipeline.explicit-factory",
            sp => new ThrowsIfConstructed_ExplicitFactoryProbe());

        // 3) AddTopic(TopicDescriptor) direct registration.
        builder.AddTopic(new TopicDescriptor(
            "cc104.pipeline.direct-descriptor",
            _ => new ThrowsIfConstructed_DirectDescriptorProbe()));

        // 4) AddTopicsFromAssemblyContaining<T>() -- discovers ThrowsIfConstructed_ScanProbeA/B
        //    (and every other concrete ITopic probe already defined in this test assembly).
        builder.AddTopicsFromAssemblyContaining<TopicRegistrationInstanceSeparationTests>();

        // CC-103's aggregated validation pass over the whole resolved set.
        TopicRegistrationValidationResult validationResult = null!;
        Action validate = () => validationResult = builder.ValidateTopicRegistrations(
            startTopicId: "cc104.pipeline.di-activation",
            fallbackTopicId: "cc104.pipeline.explicit-factory");
        validate.Should().NotThrow();
        validationResult.IsValid.Should().BeTrue(
            "every designated topic ID was registered and no topic ID collided, so validation should report success");

        // Finally, resolve the descriptor set the way a consumer (eventually CC-202's
        // ITopicCatalog) would: IEnumerable<TopicDescriptor> constructor injection.
        using var provider = builder.Services.BuildServiceProvider();
        List<TopicDescriptor> descriptors = null!;
        Action resolve = () => descriptors = provider.GetServices<TopicDescriptor>().ToList();
        resolve.Should().NotThrow();

        // The full set was actually registered -- this isn't a vacuously passing test.
        descriptors.Select(d => d.TopicId).Should().Contain(new[]
        {
            "cc104.pipeline.di-activation",
            "cc104.pipeline.explicit-factory",
            "cc104.pipeline.direct-descriptor",
            typeof(ThrowsIfConstructed_ScanProbeA).FullName!,
            typeof(ThrowsIfConstructed_ScanProbeB).FullName!
        });
    }

    // ─────────────────────────────────────────────────────────────────
    // Sanity check: prove the throwing probes and the resolution pipeline are not
    // trivially inert -- explicitly invoking a resolved descriptor's Factory *does*
    // construct (and, for these probes, throw), confirming the "NotThrow" assertions
    // above are meaningful rather than accidentally never exercising construction at all.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SanityCheck_ExplicitlyInvokingResolvedDescriptorFactory_DoesConstructAndThrow()
    {
        var builder = NewBuilder();
        builder.AddTopic<ThrowsIfConstructed_DiActivationProbe>("cc104.sanity-check");

        using var provider = builder.Services.BuildServiceProvider();
        var descriptor = provider.GetServices<TopicDescriptor>().Single(d => d.TopicId == "cc104.sanity-check");

        Action act = () => descriptor.Factory(provider);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(ThrowsIfConstructed_DiActivationProbe)} was constructed*");
    }
}
