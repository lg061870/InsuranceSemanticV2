using System;
using System.Collections.Generic;
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
/// Coverage for <see cref="TopicActivator"/> (CC-203): successful activation invoking the
/// descriptor's factory with the given service provider, activating an unregistered topic
/// ID (the error path), awaiting an <see cref="IAsyncInitializable"/>-implementing topic's
/// initialization before the activation task completes, a topic that does not implement
/// that optional seam activating immediately without error, cancellation token
/// propagation into <see cref="IAsyncInitializable.InitializeAsync"/>, and the
/// exactly-once-factory-invocation / same-instance-returned sanity checks.
/// </summary>
public class TopicActivatorTests
{
    // ─────────────────────────────────────────────────────────────────
    // Fakes/probes
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal <see cref="ITopic"/> double implementing no optional seam. Used to prove
    /// activation of an ordinary topic (the common case for every real topic today)
    /// succeeds immediately without requiring <see cref="IAsyncInitializable"/>.
    /// </summary>
    private sealed class FakeTopic : ITopic
    {
        public string Name => "FakeTopic";
        public int Priority => 0;

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("FakeTopic is not meant to be executed by TopicActivator tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("FakeTopic is not meant to be executed by TopicActivator tests.");
    }

    /// <summary>
    /// Probe implementing the optional <see cref="IAsyncInitializable"/> seam this ticket
    /// defines. <see cref="InitializeAsync"/> signals <see cref="EnteredInitialize"/> as
    /// soon as it is invoked (so a test can deterministically wait until activation has
    /// reached the await point, with no arbitrary sleep) and then awaits an
    /// externally-controlled gate task before completing, so a test can prove the
    /// activation task does not complete until that gate is released.
    /// </summary>
    private sealed class AsyncInitProbeTopic : ITopic, IAsyncInitializable
    {
        private readonly TaskCompletionSource _entered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _gate;

        public AsyncInitProbeTopic(Task gate) => _gate = gate;

        /// <summary>Completes as soon as <see cref="InitializeAsync"/> is invoked.</summary>
        public Task EnteredInitialize => _entered.Task;

        /// <summary><see langword="true"/> once <see cref="InitializeAsync"/> has fully completed.</summary>
        public bool Completed { get; private set; }

        /// <summary>The cancellation token <see cref="InitializeAsync"/> was invoked with.</summary>
        public CancellationToken ObservedCancellationToken { get; private set; }

        public string Name => nameof(AsyncInitProbeTopic);
        public int Priority => 0;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ObservedCancellationToken = cancellationToken;
            _entered.TrySetResult();
            await _gate.ConfigureAwait(false);
            Completed = true;
        }

        public Task<TopicResult> ProcessMessageAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");

        public Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Probe topics are not meant to be executed by these tests.");
    }

    /// <summary>
    /// A minimal, real <see cref="IServiceProvider"/> (not a fake with unimplemented
    /// members) used so tests can assert the exact instance passed to
    /// <see cref="TopicDescriptor.Factory"/> is the one <see cref="ITopicActivator.ActivateAsync"/>
    /// received, without depending on any registered services.
    /// </summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private static readonly IServiceProvider NullServiceProvider = new EmptyServiceProvider();

    private static TopicActivator MakeActivator(params TopicDescriptor[] descriptors) =>
        new(new TopicCatalog(descriptors));

    // ─────────────────────────────────────────────────────────────────
    // Successful activation
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateAsync_WithRegisteredTopicId_InvokesFactory_WithTheGivenServiceProvider()
    {
        IServiceProvider? capturedProvider = null;
        var produced = new FakeTopic();
        var descriptor = new TopicDescriptor("probe.factory-args", sp =>
        {
            capturedProvider = sp;
            return produced;
        });
        var activator = MakeActivator(descriptor);

        var result = await activator.ActivateAsync("probe.factory-args", NullServiceProvider);

        result.Should().BeSameAs(produced);
        capturedProvider.Should().BeSameAs(NullServiceProvider);
    }

    [Fact]
    public async Task ActivateAsync_InvokesFactoryExactlyOnce_AndReturnsTheExactInstanceItProduced()
    {
        var callCount = 0;
        var produced = new FakeTopic();
        var descriptor = new TopicDescriptor("probe.once", _ =>
        {
            callCount++;
            return produced;
        });
        var activator = MakeActivator(descriptor);

        var result = await activator.ActivateAsync("probe.once", NullServiceProvider);

        callCount.Should().Be(1);
        result.Should().BeSameAs(produced);
    }

    [Fact]
    public async Task ActivateAsync_WithTopicNotImplementingIAsyncInitializable_ActivatesImmediately_WithoutError()
    {
        var descriptor = new TopicDescriptor("probe.sync", _ => new FakeTopic());
        var activator = MakeActivator(descriptor);

        var result = await activator.ActivateAsync("probe.sync", NullServiceProvider);

        result.Should().BeOfType<FakeTopic>();
    }

    [Fact]
    public async Task ActivateAsync_IsCaseInsensitiveForTopicId_MatchingTheCatalogsLookupSemantics()
    {
        var produced = new FakeTopic();
        var descriptor = new TopicDescriptor("Probe.Case", _ => produced);
        var activator = MakeActivator(descriptor);

        var result = await activator.ActivateAsync("PROBE.CASE", NullServiceProvider);

        result.Should().BeSameAs(produced);
    }

    // ─────────────────────────────────────────────────────────────────
    // Awaiting IAsyncInitializable: activation cannot race initialization
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateAsync_WithAsyncInitializableTopic_DoesNotCompleteUntilInitializeAsyncCompletes()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new AsyncInitProbeTopic(gate.Task);
        var descriptor = new TopicDescriptor("probe.async-init", _ => probe);
        var activator = MakeActivator(descriptor);

        var activateTask = activator.ActivateAsync("probe.async-init", NullServiceProvider);

        // Deterministically wait until InitializeAsync has actually been entered, rather
        // than relying on an arbitrary sleep, then prove the activation task is still
        // pending while the probe's own initialization gate has not been released.
        await probe.EnteredInitialize.WaitAsync(TimeSpan.FromSeconds(5));
        activateTask.IsCompleted.Should().BeFalse(
            "the activation task must not complete while the topic's IAsyncInitializable.InitializeAsync is still pending");

        gate.SetResult();
        var result = await activateTask.WaitAsync(TimeSpan.FromSeconds(5));

        result.Should().BeSameAs(probe);
        probe.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task ActivateAsync_PropagatesCancellationToken_IntoInitializeAsync()
    {
        var probe = new AsyncInitProbeTopic(Task.CompletedTask);
        var descriptor = new TopicDescriptor("probe.cancel-token", _ => probe);
        var activator = MakeActivator(descriptor);
        using var cts = new CancellationTokenSource();

        await activator.ActivateAsync("probe.cancel-token", NullServiceProvider, cts.Token);

        probe.ObservedCancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task ActivateAsync_WithAlreadyCanceledToken_ThrowsWithoutInvokingFactory()
    {
        var invoked = false;
        var descriptor = new TopicDescriptor("probe.pre-canceled", _ =>
        {
            invoked = true;
            return new FakeTopic();
        });
        var activator = MakeActivator(descriptor);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => activator.ActivateAsync("probe.pre-canceled", NullServiceProvider, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        invoked.Should().BeFalse("a canceled token must be honored before the factory runs");
    }

    // ─────────────────────────────────────────────────────────────────
    // Unregistered topic ID: a real error, not a normal "not found"
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateAsync_WithUnregisteredTopicId_ThrowsTopicActivationException()
    {
        var activator = MakeActivator();

        Func<Task> act = () => activator.ActivateAsync("no.such.topic", NullServiceProvider);

        var assertion = await act.Should().ThrowAsync<TopicActivationException>();
        assertion.Which.TopicId.Should().Be("no.such.topic");
    }

    [Fact]
    public async Task ActivateAsync_WhenFactoryReturnsNull_ThrowsTopicActivationException()
    {
        var descriptor = new TopicDescriptor("probe.null-factory", _ => null!);
        var activator = MakeActivator(descriptor);

        Func<Task> act = () => activator.ActivateAsync("probe.null-factory", NullServiceProvider);

        var assertion = await act.Should().ThrowAsync<TopicActivationException>();
        assertion.Which.TopicId.Should().Be("probe.null-factory");
    }

    // ─────────────────────────────────────────────────────────────────
    // Argument validation
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ActivateAsync_WithNullEmptyOrWhitespaceTopicId_ThrowsArgumentException(string? topicId)
    {
        var activator = MakeActivator();

        Func<Task> act = () => activator.ActivateAsync(topicId!, NullServiceProvider);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ActivateAsync_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        var descriptor = new TopicDescriptor("probe.null-provider", _ => new FakeTopic());
        var activator = MakeActivator(descriptor);

        Func<Task> act = () => activator.ActivateAsync("probe.null-provider", null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullCatalog_ThrowsArgumentNullException()
    {
        Action act = () => new TopicActivator(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
