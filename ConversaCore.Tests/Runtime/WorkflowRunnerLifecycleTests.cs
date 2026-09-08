using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.Topics;

namespace ConversaCore.Tests.Runtime;

/// <summary>Focused lifecycle coverage for the framework-owned workflow runner (CC-211).</summary>
public sealed class WorkflowRunnerLifecycleTests
{
    private static readonly IServiceProvider Services = new EmptyServiceProvider();

    [Fact]
    public async Task StartAsync_ActivatesRunsAndReleasesCompletedTopic()
    {
        var topic = new ScriptedTopic("start", _ => Completed("started"));
        var descriptor = Descriptor("start", () => topic);
        var (runner, session, outputs) = CreateRunner(descriptor);
        using (runner)
        {
            var outcome = await runner.StartAsync(descriptor);

            Assert.Equal(WorkflowExecutionState.Completed, outcome.State);
            Assert.Equal("started", outcome.Response);
            Assert.Equal([string.Empty], topic.Messages);
            Assert.False(runner.HasActiveExecution);
            Assert.Null(session.ActiveTopic);
            Assert.Equal([outcome], outputs.Outcomes);
        }
    }

    [Fact]
    public async Task WaitingTopic_IsRetainedAndResumedByTheNextMessage()
    {
        var topic = new ScriptedTopic(
            "waiting",
            _ => Waiting("question"),
            message => Completed($"answer:{message}"));
        var descriptor = Descriptor("waiting", () => topic);
        var (runner, session, _) = CreateRunner(descriptor);
        using (runner)
        {
            var waiting = await runner.StartAsync(descriptor);

            Assert.Equal(WorkflowExecutionState.WaitingForInput, waiting.State);
            Assert.True(runner.HasActiveExecution);
            Assert.Same(descriptor, session.ActiveTopic);

            var completed = await runner.DeliverToActiveAsync("customer answer");

            Assert.Equal(WorkflowExecutionState.Completed, completed.State);
            Assert.Equal("answer:customer answer", completed.Response);
            Assert.Equal([string.Empty, "customer answer"], topic.Messages);
            Assert.False(runner.HasActiveExecution);
            Assert.Null(session.ActiveTopic);
        }
    }

    [Fact]
    public async Task NestedSubtopics_CompleteInsideOutAndResumeEachParentExactlyOnce()
    {
        var parent = new ScriptedTopic(
            "parent",
            _ => Subtopic("child-a"),
            _ => Completed("parent complete"));
        var childA = new ScriptedTopic(
            "child-a",
            _ => Subtopic("child-b"),
            _ => Completed("child-a complete"));
        var childB = new ScriptedTopic("child-b", _ => Completed("child-b complete"));
        var parentDescriptor = Descriptor("parent", () => parent);
        var childADescriptor = Descriptor("child-a", () => childA);
        var childBDescriptor = Descriptor("child-b", () => childB);
        var (runner, session, outputs) = CreateRunner(parentDescriptor, childADescriptor, childBDescriptor);
        using (runner)
        {
            var outcome = await runner.StartAsync(parentDescriptor);

            Assert.Equal(WorkflowExecutionState.Completed, outcome.State);
            Assert.Same(parentDescriptor, outcome.Topic);
            Assert.Equal([string.Empty, "Sub-topic completed"], parent.Messages);
            Assert.Equal([string.Empty, "Sub-topic completed"], childA.Messages);
            Assert.Equal([string.Empty], childB.Messages);
            Assert.Equal(0, runner.PendingSubtopicDepth);
            Assert.Equal(0, session.TopicCallDepth);
            Assert.False(runner.HasActiveExecution);
            Assert.Equal(
                [
                    ("parent", WorkflowExecutionState.WaitingForSubtopic),
                    ("child-a", WorkflowExecutionState.WaitingForSubtopic),
                    ("child-b", WorkflowExecutionState.Completed),
                    ("child-a", WorkflowExecutionState.Completed),
                    ("parent", WorkflowExecutionState.Completed)
                ],
                outputs.Outcomes.Select(output => (output.Topic.TopicId, output.State)));
        }
    }

    [Fact]
    public async Task FallbackInterruption_CompletesThenResumesOriginalActivation()
    {
        var original = new ScriptedTopic(
            "original",
            _ => Waiting("original waiting"),
            _ => Waiting("original resumed"));
        var fallback = new ScriptedTopic("fallback", message => Completed($"fallback:{message}"));
        var originalDescriptor = Descriptor("original", () => original);
        var fallbackDescriptor = Descriptor("fallback", () => fallback, TopicClassification.System);
        var (runner, session, outputs) = CreateRunner(originalDescriptor, fallbackDescriptor);
        using (runner)
        {
            await runner.StartAsync(originalDescriptor);
            var outcome = await runner.InterruptAndDeliverAsync(fallbackDescriptor, "unmatched request");

            Assert.Equal(WorkflowExecutionState.WaitingForInput, outcome.State);
            Assert.Same(originalDescriptor, outcome.Topic);
            Assert.Same(originalDescriptor, session.ActiveTopic);
            Assert.Equal([string.Empty, "Interrupted topic completed"], original.Messages);
            Assert.Equal(["unmatched request"], fallback.Messages);
            Assert.True(runner.HasActiveExecution);
            Assert.Equal(0, runner.PendingSubtopicDepth);
            Assert.Equal(
                [
                    ("original", WorkflowExecutionState.WaitingForInput),
                    ("fallback", WorkflowExecutionState.Completed),
                    ("original", WorkflowExecutionState.WaitingForInput)
                ],
                outputs.Outcomes.Select(output => (output.Topic.TopicId, output.State)));
        }
    }

    [Fact]
    public async Task CancelAsync_CancelsInFlightExecutionAndClearsRetainedState()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var topic = new ScriptedTopic("blocking", async (_, token) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Completed("unreachable");
        });
        var descriptor = Descriptor("blocking", () => topic);
        var (runner, session, _) = CreateRunner(descriptor);
        using (runner)
        {
            var execution = runner.StartAsync(descriptor);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var cancellation = runner.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            await cancellation;
            Assert.False(runner.HasActiveExecution);
            Assert.Equal(0, runner.PendingSubtopicDepth);
            Assert.Null(session.ActiveTopic);
        }
    }

    [Fact]
    public async Task ResetAsync_ClearsExecutionAndConversationState()
    {
        var topic = new ScriptedTopic("waiting", _ => Waiting("waiting"));
        var descriptor = Descriptor("waiting", () => topic);
        var (runner, session, _) = CreateRunner(descriptor);
        using (runner)
        {
            session.SetValue("shared", "value");
            session.RegisterPendingHostInteraction("request-1");
            await runner.StartAsync(descriptor);

            await runner.ResetAsync();

            Assert.False(runner.HasActiveExecution);
            Assert.Equal(0, runner.PendingSubtopicDepth);
            Assert.Null(session.ActiveTopic);
            Assert.Empty(session.TopicHistory);
            Assert.Equal(0, session.TopicCallDepth);
            Assert.False(session.HasValue("shared"));
            Assert.Empty(session.PendingHostInteractionIds);
        }
    }

    [Fact]
    public async Task TopicFailure_IsPropagatedWithoutTranslation()
    {
        var failure = new ApplicationException("topic failed");
        var topic = new ScriptedTopic("failing", (_, _) => Task.FromException<TopicResult>(failure));
        var descriptor = Descriptor("failing", () => topic);
        var (runner, _, outputs) = CreateRunner(descriptor);
        using (runner)
        {
            var thrown = await Assert.ThrowsAsync<ApplicationException>(() => runner.StartAsync(descriptor));

            Assert.Same(failure, thrown);
            Assert.Empty(outputs.Outcomes);
        }
    }

    [Fact]
    public async Task RepeatedActivation_CreatesAndRunsAFreshTopicInstanceEachTime()
    {
        var activations = new List<ScriptedTopic>();
        var descriptor = new TopicDescriptor("repeat", _ =>
        {
            var activationNumber = activations.Count + 1;
            var topic = new ScriptedTopic("repeat", _ => Completed($"activation-{activationNumber}"));
            activations.Add(topic);
            return topic;
        });
        var (runner, session, _) = CreateRunner(descriptor);
        using (runner)
        {
            var first = await runner.StartAsync(descriptor);
            var second = await runner.StartAsync(descriptor);

            Assert.Equal("activation-1", first.Response);
            Assert.Equal("activation-2", second.Response);
            Assert.Equal(2, activations.Count);
            Assert.NotSame(activations[0], activations[1]);
            Assert.All(activations, topic => Assert.Equal([string.Empty], topic.Messages));
            Assert.Equal(["repeat", "repeat"], session.TopicHistory);
        }
    }

    private static (WorkflowRunner Runner, ConversationSession Session, RecordingDispatcher Outputs) CreateRunner(
        params TopicDescriptor[] descriptors)
    {
        var catalog = new TopicCatalog(descriptors);
        var session = new ConversationSession(new ConversationContext("conversation-1", "subject-1"));
        var outputs = new RecordingDispatcher();
        var runner = new WorkflowRunner(session, new TopicActivator(catalog), catalog, Services, outputs);
        return (runner, session, outputs);
    }

    private static TopicDescriptor Descriptor(
        string topicId,
        Func<ITopic> factory,
        TopicClassification classification = TopicClassification.Domain) =>
        new(topicId, _ => factory()) { Classification = classification };

    private static TopicResult Completed(string response) => new()
    {
        Response = response,
        IsHandled = true,
        IsCompleted = true
    };

    private static TopicResult Waiting(string response) => new()
    {
        Response = response,
        IsHandled = true,
        RequiresInput = true
    };

    private static TopicResult Subtopic(string topicId) => new()
    {
        IsHandled = true,
        KeepActive = true,
        NextTopicName = topicId
    };

    private sealed class ScriptedTopic : ITopic
    {
        private readonly Queue<Func<string, CancellationToken, Task<TopicResult>>> _steps;

        public ScriptedTopic(string name, params Func<string, TopicResult>[] steps)
            : this(name, steps.Select<Func<string, TopicResult>, Func<string, CancellationToken, Task<TopicResult>>>(
                step => (message, _) => Task.FromResult(step(message))).ToArray())
        {
        }

        public ScriptedTopic(string name, params Func<string, CancellationToken, Task<TopicResult>>[] steps)
        {
            Name = name;
            _steps = new Queue<Func<string, CancellationToken, Task<TopicResult>>>(steps);
        }

        public string Name { get; }
        public int Priority => 0;
        public List<string> Messages { get; } = [];

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            Assert.NotEmpty(_steps);
            return _steps.Dequeue()(message, cancellationToken);
        }

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(1f);
    }

    private sealed class RecordingDispatcher : IWorkflowOutputDispatcher
    {
        public List<WorkflowExecutionOutcome> Outcomes { get; } = [];

        public Task DispatchAsync(WorkflowExecutionOutcome outcome, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Outcomes.Add(outcome);
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
