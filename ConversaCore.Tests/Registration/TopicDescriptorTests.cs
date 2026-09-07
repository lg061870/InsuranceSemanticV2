using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.Models; // TopicResult
using ConversaCore.Registration;
using ConversaCore.Topics; // ITopic
using FluentAssertions;
using Xunit;

namespace ConversaCore.Tests.Registration;

/// <summary>
/// Coverage for <see cref="TopicDescriptor"/>: construction with valid data, rejection
/// of an invalid stable ID, default values for optional fields, and equality/identity
/// semantics suitable for dictionary-keying and duplicate-registration detection.
/// </summary>
public class TopicDescriptorTests {
    /// <summary>
    /// Minimal <see cref="ITopic"/> double used only so a real
    /// <c>Func&lt;IServiceProvider, ITopic&gt;</c> factory delegate can be constructed.
    /// Its members are never invoked by these tests: <see cref="TopicDescriptor"/>
    /// stores the factory but does not call it.
    /// </summary>
    private sealed class FakeTopic : ITopic {
        public string Name => "FakeTopic";
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("FakeTopic is not meant to be executed by TopicDescriptor tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("FakeTopic is not meant to be executed by TopicDescriptor tests.");
    }

    private static readonly Func<IServiceProvider, ITopic> Factory = _ => new FakeTopic();

    [Fact]
    public void Constructor_WithValidTopicIdAndFactory_SetsTopicIdAndFactory() {
        var descriptor = new TopicDescriptor("insurance.marketing.t1", Factory);

        descriptor.TopicId.Should().Be("insurance.marketing.t1");
        descriptor.Factory.Should().BeSameAs(Factory);
    }

    [Fact]
    public void Constructor_WithoutDisplayName_DefaultsDisplayNameToTopicId() {
        var descriptor = new TopicDescriptor("insurance.marketing.t1", Factory);

        descriptor.DisplayName.Should().Be("insurance.marketing.t1");
    }

    [Fact]
    public void Constructor_WithWhitespaceDisplayName_DefaultsDisplayNameToTopicId() {
        var descriptor = new TopicDescriptor("insurance.marketing.t1", Factory, displayName: "   ");

        descriptor.DisplayName.Should().Be("insurance.marketing.t1");
    }

    [Fact]
    public void Constructor_WithExplicitDisplayName_UsesSuppliedDisplayName() {
        var descriptor = new TopicDescriptor("insurance.marketing.t1", Factory, displayName: "Marketing Path Type 1");

        descriptor.DisplayName.Should().Be("Marketing Path Type 1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithNullEmptyOrWhitespaceTopicId_ThrowsArgumentException(string? invalidTopicId) {
        Action act = () => new TopicDescriptor(invalidTopicId!, Factory);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("topicId");
    }

    [Fact]
    public void Constructor_WithNullFactory_ThrowsArgumentNullException() {
        Action act = () => new TopicDescriptor("insurance.marketing.t1", null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("factory");
    }

    [Fact]
    public void Constructor_WhenOptionalFieldsOmitted_AppliesDocumentedDefaults() {
        var descriptor = new TopicDescriptor("insurance.marketing.t1", Factory);

        descriptor.Description.Should().BeEmpty();
        descriptor.Priority.Should().Be(0);
        descriptor.Classification.Should().Be(TopicClassification.Domain);
        descriptor.InterruptionPolicy.Should().Be(TopicInterruptionPolicy.FirstRefusal);
        descriptor.AllowedToolIds.Should().NotBeNull();
        descriptor.AllowedToolIds.Should().BeEmpty();
    }

    [Fact]
    public void InitProperties_WhenSupplied_OverrideDefaults() {
        var descriptor = new TopicDescriptor("insurance.marketing.t1", Factory, "Marketing Path Type 1") {
            Description = "Full-consent lead qualification sequence.",
            Priority = 5,
            Classification = TopicClassification.System,
            InterruptionPolicy = TopicInterruptionPolicy.Interruptible,
            AllowedToolIds = new HashSet<string> { "tool.lookup-appointment", "tool.book-appointment" }
        };

        descriptor.Description.Should().Be("Full-consent lead qualification sequence.");
        descriptor.Priority.Should().Be(5);
        descriptor.Classification.Should().Be(TopicClassification.System);
        descriptor.InterruptionPolicy.Should().Be(TopicInterruptionPolicy.Interruptible);
        descriptor.AllowedToolIds.Should().BeEquivalentTo(new[] { "tool.lookup-appointment", "tool.book-appointment" });
    }

    [Fact]
    public void Equals_SameTopicIdDifferentCasing_AreEqual() {
        var first = new TopicDescriptor("Insurance.Marketing.T1", Factory);
        var second = new TopicDescriptor("insurance.marketing.t1", Factory) { Priority = 99 };

        first.Equals(second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentTopicId_AreNotEqual() {
        var first = new TopicDescriptor("insurance.marketing.t1", Factory);
        var second = new TopicDescriptor("insurance.marketing.t2", Factory);

        first.Equals(second).Should().BeFalse();
    }

    [Fact]
    public void Equals_AgainstNull_ReturnsFalse() {
        var descriptor = new TopicDescriptor("insurance.marketing.t1", Factory);

        descriptor.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void HashSet_TreatsDescriptorsWithSameTopicIdAsDuplicates() {
        // Mirrors the WP0 duplicate-registration defect this type must guard against:
        // two descriptors that differ only in display name/other metadata must still
        // collide as the same registration when keyed by TopicId.
        var descriptors = new HashSet<TopicDescriptor> {
            new("insurance.marketing.t1", Factory, "First registration"),
            new("INSURANCE.MARKETING.T1", Factory, "Duplicate registration, different casing/display name")
        };

        descriptors.Should().ContainSingle();
    }

    [Fact]
    public void Dictionary_CanUseDescriptorAsKey() {
        var descriptor = new TopicDescriptor("insurance.marketing.t1", Factory);
        var lookup = new Dictionary<TopicDescriptor, string> {
            [descriptor] = "value"
        };

        lookup[new TopicDescriptor("insurance.marketing.t1", Factory)].Should().Be("value");
    }
}
