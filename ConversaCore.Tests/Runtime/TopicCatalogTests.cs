using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Models; // TopicResult
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.Topics; // ITopic
using FluentAssertions;
using Xunit;

namespace ConversaCore.Tests.Runtime;

/// <summary>
/// Coverage for <see cref="TopicCatalog"/> (CC-202): lookup by ID (including
/// case-insensitivity), lookup of a nonexistent ID, full enumeration, duplicate-ID
/// construction behavior, immutability against later mutation of the constructor's input
/// collection, and the no-premature-instantiation guarantee (this catalog must never
/// invoke <see cref="TopicDescriptor.Factory"/>).
/// </summary>
public class TopicCatalogTests
{
    // ─────────────────────────────────────────────────────────────────
    // Fakes/probes
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal <see cref="ITopic"/> double used only so a real
    /// <c>Func&lt;IServiceProvider, ITopic&gt;</c> factory delegate can be constructed.
    /// Its members are never invoked by these tests: <see cref="TopicDescriptor"/> stores
    /// the factory but does not call it, and neither does <see cref="TopicCatalog"/>.
    /// </summary>
    private sealed class FakeTopic : ITopic
    {
        public string Name => "FakeTopic";
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("FakeTopic is not meant to be executed by TopicCatalog tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("FakeTopic is not meant to be executed by TopicCatalog tests.");
    }

    /// <summary>
    /// Probe whose constructor throws if actually invoked. Used the same way
    /// <c>TopicRegistrationInstanceSeparationTests</c> (CC-104) proves registration and
    /// validation never construct a topic instance: an accidental
    /// <see cref="TopicDescriptor.Factory"/> invocation anywhere in
    /// <see cref="TopicCatalog"/>'s construction or lookup path fails immediately, at the
    /// exact moment it would have happened, rather than only being inferred after the
    /// fact from a counter.
    /// </summary>
    private sealed class ThrowsIfConstructedProbe : ITopic
    {
        public ThrowsIfConstructedProbe() =>
            throw new InvalidOperationException(
                $"{nameof(ThrowsIfConstructedProbe)} was constructed. TopicCatalog must never invoke " +
                "TopicDescriptor.Factory; only an explicit, caller-initiated Factory(...) call may " +
                "construct a topic instance.");

        public string Name => nameof(ThrowsIfConstructedProbe);
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    private static readonly Func<IServiceProvider, ITopic> Factory = _ => new FakeTopic();

    private static TopicDescriptor MakeDescriptor(string topicId, string? displayName = null) =>
        new(topicId, Factory, displayName);

    // ─────────────────────────────────────────────────────────────────
    // Lookup by ID
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void TryGetDescriptor_WithRegisteredId_ReturnsTrueAndTheMatchingDescriptor()
    {
        var descriptor = MakeDescriptor("insurance.marketing.t1");
        var catalog = new TopicCatalog(new[] { descriptor });

        var found = catalog.TryGetDescriptor("insurance.marketing.t1", out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(descriptor);
    }

    [Theory]
    [InlineData("INSURANCE.MARKETING.T1")]
    [InlineData("Insurance.Marketing.T1")]
    public void TryGetDescriptor_IsCaseInsensitive(string queryId)
    {
        var descriptor = MakeDescriptor("insurance.marketing.t1");
        var catalog = new TopicCatalog(new[] { descriptor });

        var found = catalog.TryGetDescriptor(queryId, out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(descriptor);
    }

    [Fact]
    public void TryGetDescriptor_WithUnregisteredId_ReturnsFalseAndNullDescriptor_WithoutThrowing()
    {
        var catalog = new TopicCatalog(new[] { MakeDescriptor("insurance.marketing.t1") });

        bool found = false;
        TopicDescriptor? result = null;
        Action act = () => found = catalog.TryGetDescriptor("no.such.topic", out result);

        act.Should().NotThrow("looking up a nonexistent topic ID is a normal query, not an error condition");
        found.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryGetDescriptor_OnEmptyCatalog_ReturnsFalse_WithoutThrowing()
    {
        var catalog = new TopicCatalog(Array.Empty<TopicDescriptor>());

        Action act = () => catalog.TryGetDescriptor("anything", out _);

        act.Should().NotThrow();
        catalog.TryGetDescriptor("anything", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetDescriptor_WithNullEmptyOrWhitespaceId_ThrowsArgumentException(string? invalidId)
    {
        var catalog = new TopicCatalog(new[] { MakeDescriptor("insurance.marketing.t1") });

        Action act = () => catalog.TryGetDescriptor(invalidId!, out _);

        act.Should().Throw<ArgumentException>().WithParameterName("topicId");
    }

    [Fact]
    public void Contains_WithRegisteredId_ReturnsTrue()
    {
        var catalog = new TopicCatalog(new[] { MakeDescriptor("insurance.marketing.t1") });

        catalog.Contains("insurance.marketing.t1").Should().BeTrue();
    }

    [Fact]
    public void Contains_IsCaseInsensitive()
    {
        var catalog = new TopicCatalog(new[] { MakeDescriptor("insurance.marketing.t1") });

        catalog.Contains("INSURANCE.MARKETING.T1").Should().BeTrue();
    }

    [Fact]
    public void Contains_WithUnregisteredId_ReturnsFalse_WithoutThrowing()
    {
        var catalog = new TopicCatalog(new[] { MakeDescriptor("insurance.marketing.t1") });

        Action act = () => catalog.Contains("no.such.topic").Should().BeFalse();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Contains_WithNullEmptyOrWhitespaceId_ThrowsArgumentException(string? invalidId)
    {
        var catalog = new TopicCatalog(new[] { MakeDescriptor("insurance.marketing.t1") });

        Action act = () => catalog.Contains(invalidId!);

        act.Should().Throw<ArgumentException>().WithParameterName("topicId");
    }

    // ─────────────────────────────────────────────────────────────────
    // Full enumeration
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Descriptors_ReturnsEveryRegisteredDescriptor()
    {
        var a = MakeDescriptor("topic.a");
        var b = MakeDescriptor("topic.b");
        var c = MakeDescriptor("topic.c");
        var catalog = new TopicCatalog(new[] { a, b, c });

        catalog.Descriptors.Should().BeEquivalentTo(new[] { a, b, c });
    }

    [Fact]
    public void Descriptors_OnEmptyInput_IsEmpty()
    {
        var catalog = new TopicCatalog(Array.Empty<TopicDescriptor>());

        catalog.Descriptors.Should().BeEmpty();
    }

    [Fact]
    public void Count_MatchesTheNumberOfRegisteredDescriptors()
    {
        var catalog = new TopicCatalog(new[]
        {
            MakeDescriptor("topic.a"),
            MakeDescriptor("topic.b")
        });

        catalog.Count.Should().Be(2);
        catalog.Count.Should().Be(catalog.Descriptors.Count);
    }

    // ─────────────────────────────────────────────────────────────────
    // Construction validation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullDescriptors_ThrowsArgumentNullException()
    {
        Action act = () => new TopicCatalog(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("descriptors");
    }

    [Fact]
    public void Constructor_WithDuplicateTopicId_ThrowsArgumentException()
    {
        Action act = () => new TopicCatalog(new[]
        {
            MakeDescriptor("topic.a", "First registration"),
            MakeDescriptor("topic.a", "Second registration")
        });

        act.Should().Throw<ArgumentException>()
            .WithParameterName("descriptors")
            .WithMessage("*topic.a*");
    }

    [Fact]
    public void Constructor_WithCaseInsensitiveDuplicateTopicId_ThrowsArgumentException()
    {
        Action act = () => new TopicCatalog(new[]
        {
            MakeDescriptor("Topic.A"),
            MakeDescriptor("topic.a")
        });

        act.Should().Throw<ArgumentException>().WithParameterName("descriptors");
    }

    [Fact]
    public void Constructor_WithNoDuplicates_DoesNotThrow()
    {
        Action act = () => new TopicCatalog(new[] { MakeDescriptor("topic.a"), MakeDescriptor("topic.b") });

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithEmptyCollection_DoesNotThrow()
    {
        Action act = () => new TopicCatalog(Array.Empty<TopicDescriptor>());

        act.Should().NotThrow();
    }

    // ─────────────────────────────────────────────────────────────────
    // Immutability: the catalog's own state does not change after construction, even if
    // the collection originally supplied to the constructor is mutated afterward.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Catalog_IsUnaffectedByLaterMutationOfTheOriginalInputList()
    {
        var original = new List<TopicDescriptor> { MakeDescriptor("topic.a") };
        var catalog = new TopicCatalog(original);

        // Mutate the list after construction: add a new descriptor and remove the original one.
        original.Add(MakeDescriptor("topic.b"));
        original.Clear();

        catalog.Count.Should().Be(1, "the catalog must have taken a defensive snapshot at construction time");
        catalog.Contains("topic.a").Should().BeTrue();
        catalog.Contains("topic.b").Should().BeFalse();
        catalog.Descriptors.Should().ContainSingle().Which.TopicId.Should().Be("topic.a");
    }

    [Fact]
    public void Descriptors_IsReadOnly_NotTheSameMutableListPassedIn()
    {
        var original = new List<TopicDescriptor> { MakeDescriptor("topic.a") };
        var catalog = new TopicCatalog(original);

        catalog.Descriptors.Should().NotBeSameAs(original);
    }

    // ─────────────────────────────────────────────────────────────────
    // No premature instantiation: TopicCatalog must never invoke TopicDescriptor.Factory.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NeverInvokesAnyDescriptorFactory()
    {
        var descriptor = new TopicDescriptor("cc202.probe", _ => new ThrowsIfConstructedProbe());

        Action act = () => new TopicCatalog(new[] { descriptor });

        act.Should().NotThrow();
    }

    [Fact]
    public void TryGetDescriptor_NeverInvokesTheResolvedDescriptorFactory()
    {
        var descriptor = new TopicDescriptor("cc202.probe", _ => new ThrowsIfConstructedProbe());
        var catalog = new TopicCatalog(new[] { descriptor });

        Action act = () => catalog.TryGetDescriptor("cc202.probe", out _);

        act.Should().NotThrow();
    }

    [Fact]
    public void Descriptors_EnumerationNeverInvokesAnyDescriptorFactory()
    {
        var descriptor = new TopicDescriptor("cc202.probe", _ => new ThrowsIfConstructedProbe());
        var catalog = new TopicCatalog(new[] { descriptor });

        Action act = () => catalog.Descriptors.ToList();

        act.Should().NotThrow();
    }

    [Fact]
    public void SanityCheck_ExplicitlyInvokingTheProbeDescriptorFactory_DoesConstructAndThrow()
    {
        var descriptor = new TopicDescriptor("cc202.probe", _ => new ThrowsIfConstructedProbe());
        var catalog = new TopicCatalog(new[] { descriptor });
        catalog.TryGetDescriptor("cc202.probe", out var resolved);

        Action act = () => resolved!.Factory(null!);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(ThrowsIfConstructedProbe)} was constructed*");
    }
}
