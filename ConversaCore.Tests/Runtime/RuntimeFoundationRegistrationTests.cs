using ConversaCore.Context;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.Topics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConversaCore.Tests.Runtime;

public sealed class RuntimeFoundationRegistrationTests
{
    [Fact]
    public void Foundation_uses_target_lifetimes_without_activating_topics_at_startup()
    {
        var activations = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IConversationContext>(_ => new ConversationContext(
            Guid.NewGuid().ToString(), "subject", NullLogger<ConversationContext>.Instance));
        new ConversaCoreBuilder(services)
            .AddTopic<ITopic>("topic", _ => { activations++; return new ProbeTopic(); })
            .AddConversationRuntimeFoundation();

        using var root = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Assert.Equal(0, activations);
        var catalog = root.GetRequiredService<ITopicCatalog>();
        Assert.Equal(0, activations);

        using var first = root.CreateScope();
        using var second = root.CreateScope();
        Assert.Same(catalog, first.ServiceProvider.GetRequiredService<ITopicCatalog>());
        Assert.Same(catalog, second.ServiceProvider.GetRequiredService<ITopicCatalog>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IConversationSession>(),
            second.ServiceProvider.GetRequiredService<IConversationSession>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IWorkflowRunner>(),
            second.ServiceProvider.GetRequiredService<IWorkflowRunner>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IConversationMessageCoordinator>(),
            second.ServiceProvider.GetRequiredService<IConversationMessageCoordinator>());
        Assert.Equal(0, activations);
    }

    private sealed class ProbeTopic : ITopic
    {
        public string Name => "topic";
        public int Priority => 0;
        public Task<ConversaCore.Models.TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
