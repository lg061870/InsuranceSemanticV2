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
/// CC-106 gap-closing coverage (ConversaCore transformation work breakdown, WP1): the one
/// coverage area named by CC-106 -- "deterministic ordering" -- that the pre-existing
/// 115 tests across <c>TopicDescriptorTests</c> (CC-101), <c>ConversaCoreBuilderTests</c>
/// (CC-100), <c>TopicRegistrationTests</c> (CC-102), <c>TopicRegistrationValidationTests</c>
/// (CC-103), <c>TopicRegistrationInstanceSeparationTests</c> (CC-104), and
/// <c>TopicCompatibilityRegistrationTests</c> (CC-105) do not exercise directly.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap.</b> <see cref="ConversaCoreBuilderTopicExtensions.AddTopicsFromAssemblyContaining{TAssemblyMarker}(ConversaCoreBuilder)"/>
/// sorts scanned candidate types via <c>.OrderBy(type =&gt; type.FullName, StringComparer.Ordinal)</c>
/// specifically "for deterministic registration order" (see that method's own inline
/// comment). <c>TopicRegistrationTests.AddTopicsFromAssemblyContaining_DiscoversConcreteProbeTopicsInThisAssembly</c>
/// and <c>TopicRegistrationInstanceSeparationTests.FullPipeline_...</c> both only assert
/// membership (<c>.Should().Contain(...)</c>), which is order-insensitive and would not
/// fail if that <c>OrderBy</c> call were ever removed or replaced with an unordered
/// <c>Assembly.GetTypes()</c> iteration. This file closes that gap with assertions on the
/// actual resulting sequence.
/// </para>
/// <para>
/// Uses local fake/probe <see cref="ITopic"/> implementations only (matching the pattern
/// used by every other file in this folder), never real InsuranceAgent topics. Probe type
/// names are declared out of alphabetical order in source (Zebra, then Mango, then Apple)
/// so a test that accidentally passed due to source-declaration order (rather than the
/// actual ordinal-by-<c>FullName</c> sort) would be caught.
/// </para>
/// </remarks>
public class TopicRegistrationOrderingTests
{
    // ─────────────────────────────────────────────────────────────────
    // Probe ITopic implementations for the assembly-scan ordering tests. Declared
    // Zebra/Mango/Apple (i.e. not already alphabetical) on purpose -- see class remarks.
    // ─────────────────────────────────────────────────────────────────

    public sealed class OrderingScanProbe_Zebra : ITopic
    {
        public string Name => nameof(OrderingScanProbe_Zebra);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by ordering tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by ordering tests.");
    }

    public sealed class OrderingScanProbe_Mango : ITopic
    {
        public string Name => nameof(OrderingScanProbe_Mango);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by ordering tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by ordering tests.");
    }

    public sealed class OrderingScanProbe_Apple : ITopic
    {
        public string Name => nameof(OrderingScanProbe_Apple);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by ordering tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by ordering tests.");
    }

    // ─────────────────────────────────────────────────────────────────
    // Probe ITopic implementations for the legacy-compatibility ordering test. These must
    // NOT be re-sorted -- AddTopicsFromLegacyRegistrations documents that it preserves the
    // host's own registration/encounter order instead.
    // ─────────────────────────────────────────────────────────────────

    public sealed class LegacyOrderProbe_Zebra : ITopic
    {
        public string Name => nameof(LegacyOrderProbe_Zebra);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by ordering tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by ordering tests.");
    }

    public sealed class LegacyOrderProbe_AppleInstance : ITopic
    {
        public LegacyOrderProbe_AppleInstance(string name) => Name = name;
        public string Name { get; }
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by ordering tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by ordering tests.");
    }

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
    // AddTopicsFromAssemblyContaining<T>(): resulting order is ordinal-by-FullName, not
    // declaration order, discovery order, or any other incidental reflection ordering.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromAssemblyContaining_RegistersScannedProbesInOrdinalFullNameOrder()
    {
        var builder = NewBuilder();

        builder.AddTopicsFromAssemblyContaining<TopicRegistrationOrderingTests>();

        var expectedOrder = new[]
        {
            typeof(OrderingScanProbe_Apple).FullName!,
            typeof(OrderingScanProbe_Mango).FullName!,
            typeof(OrderingScanProbe_Zebra).FullName!
        };

        // The whole-assembly scan also registers every other ITopic probe defined
        // elsewhere in this test assembly, so filter the resolved sequence down to just
        // these three IDs (preserving their relative order in the full result) rather than
        // asserting an exact full-list match, which would couple this test to every other
        // registration test file's probe set.
        var actualRelativeOrder = ResolveDescriptors(builder)
            .Select(d => d.TopicId)
            .Where(id => expectedOrder.Contains(id, StringComparer.Ordinal))
            .ToList();

        actualRelativeOrder.Should().Equal(expectedOrder,
            "AddTopicsFromAssemblyContaining<T>() sorts candidates via " +
            "OrderBy(type => type.FullName, StringComparer.Ordinal) specifically for " +
            "deterministic registration order");
    }

    [Fact]
    public void AddTopicsFromAssemblyContaining_ProducesTheSameOrderAcrossIndependentBuilders()
    {
        // Two separate ConversaCoreBuilder/IServiceCollection instances, scanned
        // independently: the full resulting descriptor sequence (not just these three
        // probes) must match exactly both times. Assembly.GetTypes() gives no ordering
        // guarantee on its own; this proves the explicit OrderBy makes the result
        // reproducible run over run, which is the actual "deterministic" guarantee CC-106
        // asks to be covered.
        var builderOne = NewBuilder();
        var builderTwo = NewBuilder();

        builderOne.AddTopicsFromAssemblyContaining<TopicRegistrationOrderingTests>();
        builderTwo.AddTopicsFromAssemblyContaining<TopicRegistrationOrderingTests>();

        var orderOne = ResolveDescriptors(builderOne).Select(d => d.TopicId).ToList();
        var orderTwo = ResolveDescriptors(builderTwo).Select(d => d.TopicId).ToList();

        orderOne.Should().Equal(orderTwo);
        orderOne.Should().NotBeEmpty("otherwise this comparison would be vacuous");
    }

    // ─────────────────────────────────────────────────────────────────
    // AddTopicsFromLegacyRegistrations(): the opposite guarantee -- resulting order
    // mirrors the host's own registration/encounter order and is explicitly NOT re-sorted
    // (see ConversaCoreBuilderCompatibilityExtensions's "Scan order" remarks).
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void AddTopicsFromLegacyRegistrations_PreservesOriginalRegistrationOrder_NotAlphabetical()
    {
        var builder = NewBuilder();

        // Registered Zebra (type-based), then a factory-based entry, then an
        // Apple-named instance -- deliberately not alphabetical, so a test that passed
        // only because the result happened to already be sorted would be exposed.
        builder.Services.AddScoped<ITopic, LegacyOrderProbe_Zebra>();
        builder.Services.AddScoped<ITopic>(sp => new LegacyOrderProbe_Zebra());
        builder.Services.AddSingleton<ITopic>(new LegacyOrderProbe_AppleInstance("apple-instance"));

        builder.AddTopicsFromLegacyRegistrations();

        var actualOrder = ResolveDescriptors(builder).Select(d => d.TopicId).ToList();
        var expectedOrder = new[]
        {
            typeof(LegacyOrderProbe_Zebra).FullName!,
            "legacy-topic-0",
            "apple-instance"
        };

        actualOrder.Should().Equal(expectedOrder,
            "AddTopicsFromLegacyRegistrations processes legacy ServiceDescriptor entries " +
            "in the host's own registration order and must not re-sort them");
    }
}
