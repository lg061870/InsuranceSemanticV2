using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.TopicFlow;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConversaCore.Tests.Runtime;

public sealed class ComposedTopicFlowTests
{
    [Fact]
    public async Task Activator_ComposesOnlyAfterDerivedConstructionCompletes()
    {
        PostConstructionProbe? created = null;
        var descriptor = new TopicDescriptor(
            "generated.main",
            _ => created = new PostConstructionProbe());
        var activator = new TopicActivator(new TopicCatalog([descriptor]));

        var activated = await activator.ActivateAsync(
            "generated.main",
            new EmptyServiceProvider());

        Assert.Same(created, activated);
        Assert.NotNull(created);
        Assert.True(created!.DependencyObservedDuringComposition);
        Assert.Equal(1, created.CompositionCount);
        Assert.Single(created.GetAllActivities());
    }

    [Fact]
    public async Task InitializeAsync_RepeatedAndConcurrentCalls_ComposeExactlyOnce()
    {
        var topic = new PostConstructionProbe();

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => topic.InitializeAsync()));
        await topic.InitializeAsync();

        Assert.Equal(1, topic.CompositionCount);
        Assert.Single(topic.GetAllActivities());
    }

    [Fact]
    public async Task InitializeAsync_FailureClearsPartialGraph_AndCanRetry()
    {
        var topic = new PostConstructionProbe(failFirstComposition: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => topic.InitializeAsync());

        Assert.Equal("composition failed", exception.Message);
        Assert.Empty(topic.GetAllActivities());

        await topic.InitializeAsync();

        Assert.Equal(2, topic.CompositionCount);
        Assert.Single(topic.GetAllActivities());
    }

    [Fact]
    public async Task InitializeAsync_PreCanceledToken_DoesNotCompose()
    {
        var topic = new PostConstructionProbe();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            topic.InitializeAsync(cancellation.Token));

        Assert.Equal(0, topic.CompositionCount);
        Assert.Empty(topic.GetAllActivities());
    }

    [Fact]
    public async Task RunAsync_DirectInstantiation_ComposesBeforeExecution()
    {
        var topic = new PostConstructionProbe();

        var result = await topic.RunAsync();

        Assert.Equal(1, topic.CompositionCount);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Reset_RebuildsWorkflowExactlyOnce()
    {
        var topic = new PostConstructionProbe();
        await topic.InitializeAsync();

        topic.Reset();

        Assert.Equal(2, topic.CompositionCount);
        Assert.Single(topic.GetAllActivities());

        await topic.InitializeAsync();
        Assert.Equal(2, topic.CompositionCount);
    }

    [Fact]
    public async Task InitializeAsync_TerminatedTopic_IsRejectedWithoutComposition()
    {
        var topic = new PostConstructionProbe();
        topic.Terminate();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => topic.InitializeAsync());

        Assert.Contains("terminated", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, topic.CompositionCount);
    }

    private sealed class PostConstructionProbe : ComposedTopicFlow
    {
        private readonly object _dependency;
        private readonly bool _failFirstComposition;

        public PostConstructionProbe(bool failFirstComposition = false)
            : base(new TopicWorkflowContext(), NullLogger<PostConstructionProbe>.Instance, "generated.main")
        {
            _dependency = new object();
            _failFirstComposition = failFirstComposition;
        }

        public int CompositionCount { get; private set; }
        public bool DependencyObservedDuringComposition { get; private set; }

        protected override void ComposeWorkflow()
        {
            CompositionCount++;
            DependencyObservedDuringComposition = _dependency is not null;
            Add(new SimpleActivity("message", "ready"));

            if (_failFirstComposition && CompositionCount == 1)
                throw new InvalidOperationException("composition failed");
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
