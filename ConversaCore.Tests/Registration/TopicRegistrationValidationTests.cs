using System;
using System.Collections.Generic;
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
/// Coverage for CC-103: aggregated startup validation of registered
/// <see cref="TopicDescriptor"/>s — duplicate topic IDs (including a duplicate injected
/// by bypassing <see cref="ConversaCoreBuilderTopicExtensions.AddTopic(ConversaCoreBuilder, TopicDescriptor)"/>'s
/// own single-registration check) and missing designated start/fallback topics, all
/// collected into one <see cref="TopicRegistrationValidationResult"/> instead of
/// failing fast on the first problem found.
/// </summary>
/// <remarks>
/// Uses local fake/probe <see cref="ITopic"/> implementations only (matching the pattern
/// used by <c>TopicDescriptorTests.FakeTopic</c> and <c>TopicRegistrationTests</c>'s
/// probe topics), never real InsuranceAgent topics. Validation must never invoke a
/// <see cref="TopicDescriptor.Factory"/> delegate — see
/// <see cref="ValidateTopicRegistrations_DoesNotInvokeAnyTopicDescriptorFactory"/>.
/// </remarks>
public class TopicRegistrationValidationTests
{
    // ─────────────────────────────────────────────────────────────────
    // Probe ITopic implementation
    // ─────────────────────────────────────────────────────────────────

    /// <summary>A minimal <see cref="ITopic"/> probe; never actually executed by these tests.</summary>
    public sealed class ProbeTopic : ITopic
    {
        public string Name => "ProbeTopic";
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by validation tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by validation tests.");
    }

    // Instance (not static) so xUnit's per-test class instantiation keeps this isolated
    // across test methods.
    private int _factoryInvocationCount;

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
    // Valid registration sets
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateTopicRegistrations_WithMatchingStartAndFallbackIds_IsValid()
    {
        var builder = NewBuilder();
        builder.AddTopic<ProbeTopic>("topic.start");
        builder.AddTopic<ProbeTopic>("topic.fallback");

        var result = builder.ValidateTopicRegistrations(startTopicId: "topic.start", fallbackTopicId: "topic.fallback");

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTopicRegistrations_WithNoStartOrFallbackIdsSupplied_IsValid()
    {
        var builder = NewBuilder();
        builder.AddTopic<ProbeTopic>("topic.only");

        var result = builder.ValidateTopicRegistrations();

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateTopicRegistrations_WithEmptyRegistrationSetAndNoDesignatedTopics_IsValid()
    {
        var builder = NewBuilder();

        var result = builder.ValidateTopicRegistrations();

        result.IsValid.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────
    // Missing start / fallback topics
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateTopicRegistrations_WithMissingStartTopic_ReportsError()
    {
        var builder = NewBuilder();
        builder.AddTopic<ProbeTopic>("topic.fallback");

        var result = builder.ValidateTopicRegistrations(startTopicId: "topic.start.missing", fallbackTopicId: "topic.fallback");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Code == TopicRegistrationValidationErrorCode.MissingStartTopic &&
            e.Message.Contains("topic.start.missing"));
    }

    [Fact]
    public void ValidateTopicRegistrations_WithMissingFallbackTopic_ReportsError()
    {
        var builder = NewBuilder();
        builder.AddTopic<ProbeTopic>("topic.start");

        var result = builder.ValidateTopicRegistrations(startTopicId: "topic.start", fallbackTopicId: "topic.fallback.missing");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Code == TopicRegistrationValidationErrorCode.MissingFallbackTopic &&
            e.Message.Contains("topic.fallback.missing"));
    }

    [Fact]
    public void ValidateTopicRegistrations_WithBothStartAndFallbackMissing_ReportsBothInOneAggregatedResult()
    {
        var builder = NewBuilder();
        builder.AddTopic<ProbeTopic>("topic.unrelated");

        var result = builder.ValidateTopicRegistrations(startTopicId: "missing.start", fallbackTopicId: "missing.fallback");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.Code == TopicRegistrationValidationErrorCode.MissingStartTopic);
        result.Errors.Should().Contain(e => e.Code == TopicRegistrationValidationErrorCode.MissingFallbackTopic);
    }

    // ─────────────────────────────────────────────────────────────────
    // Duplicate topic IDs — including a duplicate that bypasses AddTopic entirely
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateTopicRegistrations_WithDuplicateIdBypassingBuilder_ReportsDuplicateError()
    {
        // Register two distinct TopicDescriptor instances sharing an ID directly on the
        // IServiceCollection, deliberately skipping ConversaCoreBuilderTopicExtensions.AddTopic
        // (and therefore its own immediate duplicate-ID throw) to prove the independent,
        // aggregated validation pass still catches it.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new TopicDescriptor("dup.bypassed", _ => new ProbeTopic()));
        services.AddSingleton(new TopicDescriptor("dup.bypassed", _ => new ProbeTopic()));
        var builder = new ConversaCoreBuilder(services);

        var result = builder.ValidateTopicRegistrations();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.Code == TopicRegistrationValidationErrorCode.DuplicateTopicId &&
            e.Message.Contains("dup.bypassed"));
    }

    [Fact]
    public void ValidateTopicRegistrations_WithDuplicateIdDifferentCasingBypassingBuilder_ReportsDuplicateError()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new TopicDescriptor("Dup.Bypassed", _ => new ProbeTopic()));
        services.AddSingleton(new TopicDescriptor("dup.bypassed", _ => new ProbeTopic()));
        var builder = new ConversaCoreBuilder(services);

        var result = builder.ValidateTopicRegistrations();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == TopicRegistrationValidationErrorCode.DuplicateTopicId);
    }

    [Fact]
    public void ValidateTopicRegistrations_WithThreeCopiesOfSameId_ReportsOneAggregatedDuplicateErrorNotThree()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new TopicDescriptor("triple.dup", _ => new ProbeTopic()));
        services.AddSingleton(new TopicDescriptor("triple.dup", _ => new ProbeTopic()));
        services.AddSingleton(new TopicDescriptor("triple.dup", _ => new ProbeTopic()));
        var builder = new ConversaCoreBuilder(services);

        var result = builder.ValidateTopicRegistrations();

        result.Errors.Should().ContainSingle(e => e.Code == TopicRegistrationValidationErrorCode.DuplicateTopicId);
    }

    [Fact]
    public void ValidateTopicRegistrations_WithDuplicateAndMissingStartTopic_ReportsBothProblemsAggregated()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new TopicDescriptor("dup.combo", _ => new ProbeTopic()));
        services.AddSingleton(new TopicDescriptor("dup.combo", _ => new ProbeTopic()));
        var builder = new ConversaCoreBuilder(services);

        var result = builder.ValidateTopicRegistrations(startTopicId: "missing.start");

        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.Code == TopicRegistrationValidationErrorCode.DuplicateTopicId);
        result.Errors.Should().Contain(e => e.Code == TopicRegistrationValidationErrorCode.MissingStartTopic);
    }

    // ─────────────────────────────────────────────────────────────────
    // Factory must never be invoked by validation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateTopicRegistrations_DoesNotInvokeAnyTopicDescriptorFactory()
    {
        var builder = NewBuilder();
        builder.AddTopic(new TopicDescriptor("probe.counted", _ =>
        {
            _factoryInvocationCount++;
            return new ProbeTopic();
        }));

        builder.ValidateTopicRegistrations(startTopicId: "probe.counted", fallbackTopicId: "probe.counted");

        _factoryInvocationCount.Should().Be(0);
    }

    // ─────────────────────────────────────────────────────────────────
    // IServiceProvider overload (a host that already called builder.Services.BuildServiceProvider())
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateTopicRegistrations_OnAlreadyBuiltServiceProvider_ValidatesResolvedDescriptors()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = new ConversaCoreBuilder(services);
        builder.AddTopic<ProbeTopic>("topic.start");

        using var provider = builder.Services.BuildServiceProvider();
        var result = provider.ValidateTopicRegistrations(startTopicId: "topic.start", fallbackTopicId: "topic.missing");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == TopicRegistrationValidationErrorCode.MissingFallbackTopic);
    }

    // ─────────────────────────────────────────────────────────────────
    // Core TopicRegistrationValidator.Validate(IEnumerable<TopicDescriptor>, ...)
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_OnPlainDescriptorList_WithDuplicateIds_ReportsDuplicate()
    {
        var descriptors = new[]
        {
            new TopicDescriptor("dup", _ => new ProbeTopic()),
            new TopicDescriptor("DUP", _ => new ProbeTopic()) // same ID per TopicDescriptor's case-insensitive equality
        };

        var result = TopicRegistrationValidator.Validate(descriptors);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == TopicRegistrationValidationErrorCode.DuplicateTopicId);
    }

    [Fact]
    public void Validate_OnPlainDescriptorList_WithNoProblems_IsValid()
    {
        var descriptors = new[]
        {
            new TopicDescriptor("a", _ => new ProbeTopic()),
            new TopicDescriptor("b", _ => new ProbeTopic())
        };

        var result = TopicRegistrationValidator.Validate(descriptors, startTopicId: "a", fallbackTopicId: "b");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullDescriptors_ThrowsArgumentNullException()
    {
        Action act = () => TopicRegistrationValidator.Validate(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("descriptors");
    }

    // ─────────────────────────────────────────────────────────────────
    // Null-argument guards on the extension methods
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateTopicRegistrations_WithNullBuilder_ThrowsArgumentNullException()
    {
        ConversaCoreBuilder builder = null!;

        Action act = () => builder.ValidateTopicRegistrations();

        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void ValidateTopicRegistrations_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        IServiceProvider provider = null!;

        Action act = () => provider.ValidateTopicRegistrations();

        act.Should().Throw<ArgumentNullException>().WithParameterName("serviceProvider");
    }

    // ─────────────────────────────────────────────────────────────────
    // TopicRegistrationValidationResult.ThrowIfInvalid() — the aggregated exception
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ThrowIfInvalid_WithProblems_ThrowsExceptionListingEveryProblem()
    {
        var builder = NewBuilder();

        var result = builder.ValidateTopicRegistrations(startTopicId: "missing.start", fallbackTopicId: "missing.fallback");

        Action act = () => result.ThrowIfInvalid();

        var exception = act.Should().Throw<TopicRegistrationValidationException>().Which;
        exception.Message.Should().Contain("missing.start");
        exception.Message.Should().Contain("missing.fallback");
        exception.Result.Should().BeSameAs(result);
    }

    [Fact]
    public void ThrowIfInvalid_WhenValid_DoesNotThrow()
    {
        var builder = NewBuilder();
        builder.AddTopic<ProbeTopic>("topic.start");

        var result = builder.ValidateTopicRegistrations(startTopicId: "topic.start");

        Action act = () => result.ThrowIfInvalid();

        act.Should().NotThrow();
    }
}
