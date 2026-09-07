using System;
using System.Collections.Generic;
using System.Linq;
using ConversaCore.Registration;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Xunit;

namespace ConversaCore.Tests.Registration;

/// <summary>
/// Coverage for CC-100: the <see cref="ConversaCoreBuilder"/> skeleton and the new
/// <see cref="ServiceCollectionExtensions.AddConversaCoreBuilder(IServiceCollection, string, string)"/>
/// entry point. Verifies that the new builder-returning entry point registers the same
/// services as the pre-existing <see cref="ServiceCollectionExtensions.AddConversaCore(IServiceCollection, string, string)"/>
/// overload, that the pre-existing overload's own signature and behavior are unaffected,
/// and that the two entry points cannot drift apart because they share one private
/// registration method.
/// </summary>
/// <remarks>
/// Registration only: no AI service is resolved and no network call is made in any test
/// here. <c>Kernel.CreateBuilder().Build()</c> and <c>AddOpenAIEmbeddingGenerator</c> only
/// construct local client objects; they do not contact OpenAI at DI-registration or
/// first-resolution time. A fake API key string such as <c>"test-api-key"</c> is
/// therefore safe and deterministic, matching the pattern already used in
/// <c>ConversaCore.Tests.Characterization.LegacySessionIsolationTests</c>.
/// </remarks>
public class ConversaCoreBuilderTests {
    private const string FakeApiKey = "test-api-key";

    private static ServiceCollection NewServiceCollectionWithLogging() {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    [Fact]
    public void AddConversaCoreBuilder_ReturnsConversaCoreBuilder() {
        var services = NewServiceCollectionWithLogging();

        var result = services.AddConversaCoreBuilder(FakeApiKey);

        result.Should().BeOfType<ConversaCoreBuilder>();
    }

    [Fact]
    public void AddConversaCoreBuilder_ServicesProperty_IsSameInstanceAsThePassedServiceCollection() {
        var services = NewServiceCollectionWithLogging();

        var builder = services.AddConversaCoreBuilder(FakeApiKey);

        builder.Services.Should().BeSameAs(services);
    }

    [Fact]
    public void AddConversaCoreBuilder_ChainedWithOrdinaryServiceCollectionExtensionMethods_RegistersBothSets() {
        var services = NewServiceCollectionWithLogging();

        var builder = services.AddConversaCoreBuilder(FakeApiKey);
        builder.Services.AddSingleton<string>("chained-registration-marker");

        using var provider = builder.Services.BuildServiceProvider();
        provider.GetRequiredService<TopicRegistry>().Should().NotBeNull();
        provider.GetRequiredService<string>().Should().Be("chained-registration-marker");
    }

    [Fact]
    public void ConversaCoreBuilder_Constructor_WithNullServices_ThrowsArgumentNullException() {
        Action act = () => new ConversaCoreBuilder(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void ConversaCoreBuilder_Configure_RegistersOptionsAndReturnsSameBuilderForChaining() {
        var services = NewServiceCollectionWithLogging();
        var builder = services.AddConversaCoreBuilder(FakeApiKey);

        var result = builder.Configure<TestOptions>(o => o.Value = "configured");

        result.Should().BeSameAs(builder);
        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TestOptions>>();
        options.Value.Value.Should().Be("configured");
    }

    public class TestOptions {
        public string? Value { get; set; }
    }

    [Fact]
    public void AddConversaCore_ExistingOverload_StillReturnsTheSameIServiceCollectionInstance() {
        var services = NewServiceCollectionWithLogging();

        var result = services.AddConversaCore(FakeApiKey);

        result.Should().BeSameAs(services);
        result.Should().BeAssignableTo<IServiceCollection>();
    }

    [Fact]
    public void AddConversaCore_ExistingOverload_WithOnlyRequiredArgument_StillCompilesAndRegisters() {
        // Guards the exact call shape used by ConversaCore.Tests.Characterization.LegacySessionIsolationTests
        // and other existing hosts: a single positional string argument, relying on the
        // embeddingModel default. This must keep resolving to the IServiceCollection-returning
        // overload, not the new builder-returning one.
        var services = NewServiceCollectionWithLogging();

        IServiceCollection result = services.AddConversaCore("unused-characterization-key");

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddConversaCore_ExistingOverload_RegistersTopicRegistryKernelAndTopicManager() {
        var services = NewServiceCollectionWithLogging();
        services.AddConversaCore(FakeApiKey);
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        provider.GetRequiredService<TopicRegistry>().Should().NotBeNull();
        provider.GetRequiredService<Kernel>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ITopicManager>().Should().NotBeNull();
    }

    [Fact]
    public void AddConversaCoreBuilder_RegistersTopicRegistryKernelAndTopicManager_SameAsAddConversaCore() {
        var services = NewServiceCollectionWithLogging();
        var builder = services.AddConversaCoreBuilder(FakeApiKey);
        using var provider = builder.Services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        provider.GetRequiredService<TopicRegistry>().Should().NotBeNull();
        provider.GetRequiredService<Kernel>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<ITopicManager>().Should().NotBeNull();
    }

    [Fact]
    public void AddConversaCore_WithMissingApiKey_ThrowsInvalidOperationException() {
        var services = NewServiceCollectionWithLogging();

        Action act = () => services.AddConversaCore("   ");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddConversaCoreBuilder_WithMissingApiKey_ThrowsInvalidOperationException() {
        var services = NewServiceCollectionWithLogging();

        Action act = () => services.AddConversaCoreBuilder("   ");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddConversaCore_AndAddConversaCoreBuilder_RegisterEquivalentServiceDescriptorSets() {
        // No behavioral drift: both entry points call the same private registration
        // method, so the resulting registrations must match by service type and
        // lifetime (implementation factories necessarily differ as delegate instances,
        // so those are intentionally excluded from the comparison).
        var viaAddConversaCore = NewServiceCollectionWithLogging();
        viaAddConversaCore.AddConversaCore(FakeApiKey);

        var viaBuilder = NewServiceCollectionWithLogging();
        viaBuilder.AddConversaCoreBuilder(FakeApiKey);

        Describe(viaAddConversaCore).Should().BeEquivalentTo(Describe(viaBuilder));

        static IEnumerable<(Type ServiceType, ServiceLifetime Lifetime)> Describe(IServiceCollection services) =>
            services
                .Select(d => (d.ServiceType, d.Lifetime))
                .OrderBy(t => t.ServiceType.FullName, StringComparer.Ordinal)
                .ThenBy(t => t.Lifetime);
    }
}
