using System.Collections.Concurrent;
using ConversaCore.Context;
using ConversaCore.Models;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.Topics;

namespace ConversaCore.Tests.Runtime;

/// <summary>
/// CC-212 acceptance coverage for conversation-scoped workflow execution. The probes use
/// explicit gates rather than timing delays so that concurrency, serialization, and
/// cancellation behavior is deterministic.
/// </summary>
public sealed class WorkflowRunnerConcurrencyTests
{
    [Fact]
    public async Task Independent_runners_execute_concurrently_without_sharing_active_topic_or_session_state()
    {
        var firstEntered = NewSignal();
        var secondEntered = NewSignal();
        var release = NewSignal();
        var firstTopic = new GatedTopic("first", firstEntered, release.Task);
        var secondTopic = new GatedTopic("second", secondEntered, release.Task);
        var firstDescriptor = Descriptor("first", _ => firstTopic);
        var secondDescriptor = Descriptor("second", _ => secondTopic);
        var catalog = new TopicCatalog([firstDescriptor, secondDescriptor]);
        var activator = new TopicActivator(catalog);
        var firstSession = Session("conversation-1");
        var secondSession = Session("conversation-2");
        firstSession.SetValue("shared-key", "first-value");
        secondSession.SetValue("shared-key", "second-value");
        var firstOutput = new RecordingDispatcher();
        var secondOutput = new RecordingDispatcher();
        using var firstRunner = Runner(firstSession, activator, catalog, firstOutput);
        using var secondRunner = Runner(secondSession, activator, catalog, secondOutput);

        var firstRun = firstRunner.ActivateAndDeliverAsync(firstDescriptor, "one");
        var secondRun = secondRunner.ActivateAndDeliverAsync(secondDescriptor, "two");
        await Task.WhenAll(firstEntered.Task, secondEntered.Task).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(firstDescriptor, firstSession.ActiveTopic);
        Assert.Same(secondDescriptor, secondSession.ActiveTopic);
        Assert.Equal("first-value", firstSession.GetValue<string>("shared-key"));
        Assert.Equal("second-value", secondSession.GetValue<string>("shared-key"));
        Assert.True(firstRunner.HasActiveExecution);
        Assert.True(secondRunner.HasActiveExecution);

        release.SetResult();
        await Task.WhenAll(firstRun, secondRun).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Collection(firstOutput.Outcomes,
            outcome => Assert.Equal("first:one", outcome.Response));
        Assert.Collection(secondOutput.Outcomes,
            outcome => Assert.Equal("second:two", outcome.Response));
    }

    [Fact]
    public async Task Independent_subtopic_runs_keep_stacks_and_outputs_in_their_own_conversations()
    {
        var parentA = Descriptor("parent-a", _ => new DelegateTopic("parent-a", (_, _) =>
            Task.FromResult(Subtopic("child-a", "a-parent-output"))));
        var childA = Descriptor("child-a", _ => new DelegateTopic("child-a", (_, _) =>
            Task.FromResult(Waiting("a-child-output"))));
        var parentB = Descriptor("parent-b", _ => new DelegateTopic("parent-b", (_, _) =>
            Task.FromResult(Subtopic("child-b", "b-parent-output"))));
        var childB = Descriptor("child-b", _ => new DelegateTopic("child-b", (_, _) =>
            Task.FromResult(Waiting("b-child-output"))));
        var catalog = new TopicCatalog([parentA, childA, parentB, childB]);
        var activator = new TopicActivator(catalog);
        var sessionA = Session("conversation-a");
        var sessionB = Session("conversation-b");
        var outputA = new RecordingDispatcher();
        var outputB = new RecordingDispatcher();
        using var runnerA = Runner(sessionA, activator, catalog, outputA);
        using var runnerB = Runner(sessionB, activator, catalog, outputB);

        await Task.WhenAll(
            runnerA.StartAsync(parentA),
            runnerB.StartAsync(parentB)).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, runnerA.PendingSubtopicDepth);
        Assert.Equal(1, runnerB.PendingSubtopicDepth);
        Assert.Equal(1, sessionA.TopicCallDepth);
        Assert.Equal(1, sessionB.TopicCallDepth);
        Assert.True(sessionA.IsTopicInCallStack("child-a"));
        Assert.False(sessionA.IsTopicInCallStack("child-b"));
        Assert.True(sessionB.IsTopicInCallStack("child-b"));
        Assert.False(sessionB.IsTopicInCallStack("child-a"));
        Assert.Same(childA, sessionA.ActiveTopic);
        Assert.Same(childB, sessionB.ActiveTopic);
        Assert.Equal(["parent-a", "child-a"], outputA.Outcomes.Select(x => x.Topic.TopicId));
        Assert.Equal(["parent-b", "child-b"], outputB.Outcomes.Select(x => x.Topic.TopicId));
    }

    [Fact]
    public async Task Commands_on_the_same_runner_are_serialized()
    {
        var firstEntered = NewSignal();
        var secondEntered = NewSignal();
        var releaseFirst = NewSignal();
        var releaseSecond = NewSignal();
        var topic = new SerializedProbeTopic(firstEntered, secondEntered, releaseFirst.Task, releaseSecond.Task);
        var descriptor = Descriptor("serialized", _ => topic);
        var catalog = new TopicCatalog([descriptor]);
        var session = Session("serialized-conversation");
        using var runner = Runner(session, new TopicActivator(catalog), catalog, new RecordingDispatcher());

        var firstCommand = runner.StartAsync(descriptor);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondCommand = runner.DeliverToActiveAsync("second-command");

        Assert.Equal(1, topic.CallCount);
        Assert.False(secondEntered.Task.IsCompleted);
        Assert.False(secondCommand.IsCompleted);

        releaseFirst.SetResult();
        await firstCommand.WaitAsync(TimeSpan.FromSeconds(5));
        await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, topic.CallCount);

        releaseSecond.SetResult();
        await secondCommand.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Reset_cancels_only_its_runner_and_preserves_another_conversations_execution_and_state()
    {
        var enteredA = NewSignal();
        var enteredB = NewSignal();
        var releaseA = NewSignal();
        var releaseB = NewSignal();
        var topicA = new GatedTopic("a", enteredA, releaseA.Task);
        var topicB = new GatedTopic("b", enteredB, releaseB.Task);
        var descriptorA = Descriptor("topic-a", _ => topicA);
        var descriptorB = Descriptor("topic-b", _ => topicB);
        var catalog = new TopicCatalog([descriptorA, descriptorB]);
        var activator = new TopicActivator(catalog);
        var sessionA = Session("conversation-a");
        var sessionB = Session("conversation-b");
        sessionA.SetValue("owner", "a");
        sessionB.SetValue("owner", "b");
        sessionA.PushTopicCall("parent-a", "topic-a");
        sessionB.PushTopicCall("parent-b", "topic-b");
        using var runnerA = Runner(sessionA, activator, catalog, new RecordingDispatcher());
        using var runnerB = Runner(sessionB, activator, catalog, new RecordingDispatcher());

        var runA = runnerA.StartAsync(descriptorA);
        var runB = runnerB.StartAsync(descriptorB);
        await Task.WhenAll(enteredA.Task, enteredB.Task).WaitAsync(TimeSpan.FromSeconds(5));

        await runnerA.ResetAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runA);

        Assert.False(runnerA.HasActiveExecution);
        Assert.Null(sessionA.ActiveTopic);
        Assert.False(sessionA.HasValue("owner"));
        Assert.Equal(0, sessionA.TopicCallDepth);
        Assert.True(runnerB.HasActiveExecution);
        Assert.Same(descriptorB, sessionB.ActiveTopic);
        Assert.Equal("b", sessionB.GetValue<string>("owner"));
        Assert.Equal(1, sessionB.TopicCallDepth);
        Assert.False(runB.IsCompleted);

        releaseB.SetResult();
        var outcomeB = await runB.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("b:", outcomeB.Response);
    }

    [Fact]
    public async Task Every_activation_gets_a_fresh_topic_instance_across_runners_and_after_reset()
    {
        var instances = new ConcurrentQueue<InstanceTopic>();
        var nextId = 0;
        var descriptor = Descriptor("fresh", _ =>
        {
            var topic = new InstanceTopic(Interlocked.Increment(ref nextId));
            instances.Enqueue(topic);
            return topic;
        });
        var catalog = new TopicCatalog([descriptor]);
        var activator = new TopicActivator(catalog);
        var firstSession = Session("conversation-1");
        var secondSession = Session("conversation-2");
        using var firstRunner = Runner(firstSession, activator, catalog, new RecordingDispatcher());
        using var secondRunner = Runner(secondSession, activator, catalog, new RecordingDispatcher());

        var initialOutcomes = await Task.WhenAll(
            firstRunner.StartAsync(descriptor),
            secondRunner.StartAsync(descriptor)).WaitAsync(TimeSpan.FromSeconds(5));
        await firstRunner.ResetAsync();
        var reactivated = await firstRunner.StartAsync(descriptor);

        Assert.Equal(3, instances.Count);
        Assert.Equal(3, instances.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Equal(3, initialOutcomes.Append(reactivated).Select(x => x.Response).Distinct().Count());
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ConversationSession Session(string conversationId) =>
        new(new ConversationContext(conversationId, $"subject-{conversationId}"));

    private static TopicDescriptor Descriptor(string id, Func<IServiceProvider, ITopic> factory) =>
        new(id, factory);

    private static WorkflowRunner Runner(
        IConversationSession session,
        ITopicActivator activator,
        ITopicCatalog catalog,
        IWorkflowOutputDispatcher output) =>
        new(session, activator, catalog, EmptyServiceProvider.Instance, output);

    private static TopicResult Waiting(string response) => new()
    {
        Response = response,
        IsHandled = true,
        RequiresInput = true,
        KeepActive = true
    };

    private static TopicResult Subtopic(string childId, string response) => new()
    {
        Response = response,
        IsHandled = true,
        KeepActive = true,
        NextTopicName = childId
    };

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static EmptyServiceProvider Instance { get; } = new();
        public object? GetService(Type serviceType) => null;
    }

    private sealed class RecordingDispatcher : IWorkflowOutputDispatcher
    {
        private readonly ConcurrentQueue<WorkflowExecutionOutcome> _outcomes = new();
        public IReadOnlyList<WorkflowExecutionOutcome> Outcomes => _outcomes.ToArray();

        public Task DispatchAsync(WorkflowExecutionOutcome outcome, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _outcomes.Enqueue(outcome);
            return Task.CompletedTask;
        }
    }

    private sealed class DelegateTopic(
        string name,
        Func<string, CancellationToken, Task<TopicResult>> process) : ITopic
    {
        public string Name => name;
        public int Priority => 0;
        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) =>
            process(message, cancellationToken);
        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(1f);
    }

    private sealed class GatedTopic(
        string name,
        TaskCompletionSource entered,
        Task release) : ITopic
    {
        public string Name => name;
        public int Priority => 0;

        public async Task<TopicResult> ProcessMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            entered.TrySetResult();
            await release.WaitAsync(cancellationToken).ConfigureAwait(false);
            return Waiting($"{name}:{message}");
        }

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(1f);
    }

    private sealed class SerializedProbeTopic(
        TaskCompletionSource firstEntered,
        TaskCompletionSource secondEntered,
        Task releaseFirst,
        Task releaseSecond) : ITopic
    {
        private int _callCount;
        public string Name => "serialized";
        public int Priority => 0;
        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<TopicResult> ProcessMessageAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                firstEntered.TrySetResult();
                await releaseFirst.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else if (call == 2)
            {
                secondEntered.TrySetResult();
                await releaseSecond.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException($"Unexpected call {call}.");
            }

            return Waiting($"call-{call}:{message}");
        }

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(1f);
    }

    private sealed class InstanceTopic(int instanceId) : ITopic
    {
        public string Name => $"instance-{instanceId}";
        public int Priority => 0;
        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(Waiting(Name));
        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(1f);
    }
}
