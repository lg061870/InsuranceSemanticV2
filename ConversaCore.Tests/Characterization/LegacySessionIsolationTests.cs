using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Tests.Characterization;

/// <summary>
/// Documents legacy defects, not the isolation contract of the replacement runtime.
/// Retain these beside independent positive isolation tests when the new catalog ships.
/// </summary>
public class LegacySessionIsolationTests {
    private static ServiceProvider CreateProvider() {
        var services = new ServiceCollection();
        services.AddLogging();
        // Registration only: no AI service is resolved and no network call is made.
        services.AddConversaCore("unused-characterization-key");
        services.AddScoped<ITopic, ProbeTopic>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void DirectScopeResolution_IsolatesTopicsAndBothContexts() {
        using var provider = CreateProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var a = (ProbeTopic)first.ServiceProvider.GetRequiredService<ITopic>();
        var b = (ProbeTopic)second.ServiceProvider.GetRequiredService<ITopic>();

        Assert.NotSame(a, b);
        Assert.NotSame(a.Conversation, b.Conversation);
        Assert.NotEqual(a.Conversation.ConversationId, b.Conversation.ConversationId);
        Assert.NotSame(a.Workflow, b.Workflow);
        a.Workflow.SetValue("answer", "first session");
        Assert.Null(b.Workflow.GetValue<string>("answer"));
    }

    [Fact]
    public void StartupRegistry_RetainsDisposedTopicAndSharesItsStateAcrossSessions() {
        using var provider = CreateProvider();
        var registry = provider.GetRequiredService<TopicRegistry>();
        ProbeTopic startupTopic;
        using (var startup = provider.CreateScope()) {
            registry.ConfigureTopics(startup.ServiceProvider);
            startupTopic = (ProbeTopic)registry.GetTopic("probe")!;
        }

        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var a = (ProbeTopic)first.ServiceProvider.GetRequiredService<TopicRegistry>().GetTopic("probe")!;
        var b = (ProbeTopic)second.ServiceProvider.GetRequiredService<TopicRegistry>().GetTopic("probe")!;

        Assert.True(startupTopic.Disposed);
        Assert.Same(startupTopic, a);
        Assert.Same(a, b);
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IConversationContext>(), a.Conversation);
        a.Workflow.SetValue("answer", "leaked across sessions");
        Assert.Equal("leaked across sessions", b.Workflow.GetValue<string>("answer"));
    }

    [Fact]
    public void Reconfiguration_DoesNotReplaceFirstTopicWithCurrentSessionInstance() {
        using var provider = CreateProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var registry = provider.GetRequiredService<TopicRegistry>();
        registry.ConfigureTopics(first.ServiceProvider);
        var original = registry.GetTopic("probe");
        registry.ConfigureTopics(second.ServiceProvider);

        Assert.Single(registry.GetAllTopics());
        Assert.Same(original, registry.GetTopic("probe"));
        Assert.NotSame(second.ServiceProvider.GetRequiredService<ITopic>(), original);
    }

    [Fact]
    public void ResetFromOneSession_ChangesRegistryObservedByAnotherSession() {
        using var provider = CreateProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var registry = provider.GetRequiredService<TopicRegistry>();
        registry.ConfigureTopics(first.ServiceProvider);
        var original = registry.GetTopic("probe");

        second.ServiceProvider.ResetConversaCore();

        Assert.NotSame(original, registry.GetTopic("probe"));
        Assert.Same(second.ServiceProvider.GetRequiredService<ITopic>(),
            first.ServiceProvider.GetRequiredService<TopicRegistry>().GetTopic("probe"));
    }

    public sealed class ProbeTopic(IConversationContext conversation, TopicWorkflowContext workflow)
        : ITopic, IDisposable {
        public IConversationContext Conversation { get; } = conversation;
        public TopicWorkflowContext Workflow { get; } = workflow;
        public bool Disposed { get; private set; }
        public string Name => "probe";
        public int Priority => 0;
        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => Task.FromResult(1f);
        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => Task.FromResult(new TopicResult());
        public void Dispose() => Disposed = true;
    }
}
