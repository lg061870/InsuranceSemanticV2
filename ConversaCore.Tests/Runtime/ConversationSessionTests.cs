using System;
using ConversaCore.Context;
using ConversaCore.Registration;
using ConversaCore.Runtime;
using FluentAssertions;
using Xunit;

namespace ConversaCore.Tests.Runtime;

/// <summary>
/// Coverage for <see cref="ConversationSession"/> (CC-201): construction/identity
/// delegation, active-topic get/set semantics, call-stack push/pop behavior delegated to
/// the composed <see cref="IConversationContext"/>, shared-state get/set, pending
/// host-interaction registration/lookup/removal including duplicate-ID and unknown-ID
/// edge cases, and <see cref="ConversationSession.Reset"/>.
/// </summary>
/// <remarks>
/// Tests compose a real <see cref="ConversationContext"/> (not a mock) as the backing
/// context, since the behavior under test is largely "does this correctly delegate to,
/// and layer new state on top of, the already-correct legacy implementation" — using the
/// real implementation exercises that composition honestly rather than asserting against
/// a hand-rolled substitute of it.
/// </remarks>
public class ConversationSessionTests
{
    private static ConversationContext CreateContext(string conversationId = "conv-1", string userId = "user-1")
        => new(conversationId, userId);

    private static TopicDescriptor CreateTopicDescriptor(string topicId)
        => new(topicId, _ => null!);

    // === Construction / identity ===

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        Action act = () => new ConversationSession(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void ConversationId_DelegatesToUnderlyingContext()
    {
        var context = CreateContext(conversationId: "conv-42");
        var session = new ConversationSession(context);

        session.ConversationId.Should().Be("conv-42");
    }

    [Fact]
    public void Subject_DelegatesToUnderlyingContextUserId()
    {
        var context = CreateContext(userId: "user-99");
        var session = new ConversationSession(context);

        session.Subject.Should().Be("user-99");
    }

    // === Active topic get/set ===

    [Fact]
    public void ActiveTopic_DefaultsToNull()
    {
        var session = new ConversationSession(CreateContext());

        session.ActiveTopic.Should().BeNull();
    }

    [Fact]
    public void SetActiveTopic_WithDescriptor_UpdatesActiveTopic()
    {
        var session = new ConversationSession(CreateContext());
        var topic = CreateTopicDescriptor("topic-a");

        session.SetActiveTopic(topic);

        session.ActiveTopic.Should().BeSameAs(topic);
    }

    [Fact]
    public void SetActiveTopic_WithDescriptor_AppendsTopicIdToHistory()
    {
        var session = new ConversationSession(CreateContext());
        var topic = CreateTopicDescriptor("topic-a");

        session.SetActiveTopic(topic);

        session.TopicHistory.Should().ContainSingle().Which.Should().Be("topic-a");
    }

    [Fact]
    public void SetActiveTopic_CalledAgainWithDifferentTopic_ReplacesActiveTopicAndAppendsHistory()
    {
        var session = new ConversationSession(CreateContext());
        session.SetActiveTopic(CreateTopicDescriptor("topic-a"));

        var topicB = CreateTopicDescriptor("topic-b");
        session.SetActiveTopic(topicB);

        session.ActiveTopic.Should().BeSameAs(topicB);
        session.TopicHistory.Should().Equal("topic-a", "topic-b");
    }

    [Fact]
    public void SetActiveTopic_WithNull_ClearsActiveTopicWithoutHistoryEntry()
    {
        var session = new ConversationSession(CreateContext());
        session.SetActiveTopic(CreateTopicDescriptor("topic-a"));

        session.SetActiveTopic(null);

        session.ActiveTopic.Should().BeNull();
        session.TopicHistory.Should().Equal("topic-a");
    }

    // === Call stack ===

    [Fact]
    public void PushTopicCall_ThenPopTopicCall_ReturnsCallInfoWithCompletionData()
    {
        var session = new ConversationSession(CreateContext());

        session.PushTopicCall("parent-topic", "child-topic", resumeData: "resume-marker");
        var result = session.PopTopicCall(completionData: "completion-marker");

        result.Should().NotBeNull();
        result!.CallingTopicName.Should().Be("parent-topic");
        result.SubTopicName.Should().Be("child-topic");
        result.ResumeData.Should().Be("resume-marker");
        result.CompletionData.Should().Be("completion-marker");
    }

    [Fact]
    public void PopTopicCall_WhenStackEmpty_ReturnsNull()
    {
        var session = new ConversationSession(CreateContext());

        session.PopTopicCall().Should().BeNull();
    }

    [Fact]
    public void TopicCallDepth_ReflectsPushesAndPops()
    {
        var session = new ConversationSession(CreateContext());

        session.TopicCallDepth.Should().Be(0);

        session.PushTopicCall("a", "b");
        session.PushTopicCall("b", "c");
        session.TopicCallDepth.Should().Be(2);

        session.PopTopicCall();
        session.TopicCallDepth.Should().Be(1);

        session.PopTopicCall();
        session.TopicCallDepth.Should().Be(0);
    }

    [Fact]
    public void IsTopicInCallStack_DetectsCallingAndSubTopicIds()
    {
        var session = new ConversationSession(CreateContext());
        session.PushTopicCall("parent-topic", "child-topic");

        session.IsTopicInCallStack("parent-topic").Should().BeTrue();
        session.IsTopicInCallStack("child-topic").Should().BeTrue();
        session.IsTopicInCallStack("unrelated-topic").Should().BeFalse();
    }

    // === Shared state ===

    [Fact]
    public void SetValue_AndGetValue_RoundTrips()
    {
        var session = new ConversationSession(CreateContext());

        session.SetValue("greeting", "hello");

        session.GetValue<string>("greeting").Should().Be("hello");
    }

    [Fact]
    public void GetValue_WithMissingKey_ReturnsDefault()
    {
        var session = new ConversationSession(CreateContext());

        session.GetValue("missing", "fallback").Should().Be("fallback");
    }

    [Fact]
    public void TryGetValue_WithExistingKey_ReturnsTrueAndValue()
    {
        var session = new ConversationSession(CreateContext());
        session.SetValue("count", 3);

        var found = session.TryGetValue<int>("count", out var value);

        found.Should().BeTrue();
        value.Should().Be(3);
    }

    [Fact]
    public void TryGetValue_WithMissingKey_ReturnsFalse()
    {
        var session = new ConversationSession(CreateContext());

        session.TryGetValue<int>("missing", out _).Should().BeFalse();
    }

    [Fact]
    public void HasValue_ReflectsPresence()
    {
        var session = new ConversationSession(CreateContext());

        session.HasValue("key").Should().BeFalse();

        session.SetValue("key", "value");

        session.HasValue("key").Should().BeTrue();
    }

    // === Pending host interactions ===

    [Fact]
    public void RegisterPendingHostInteraction_MakesItPendingAndVisibleInSnapshot()
    {
        var session = new ConversationSession(CreateContext());

        session.RegisterPendingHostInteraction("req-1");

        session.IsHostInteractionPending("req-1").Should().BeTrue();
        session.PendingHostInteractionIds.Should().Contain("req-1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterPendingHostInteraction_WithInvalidId_ThrowsArgumentException(string? invalidId)
    {
        var session = new ConversationSession(CreateContext());

        Action act = () => session.RegisterPendingHostInteraction(invalidId!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("requestId");
    }

    [Fact]
    public void RegisterPendingHostInteraction_WithDuplicateId_ThrowsInvalidOperationException()
    {
        var session = new ConversationSession(CreateContext());
        session.RegisterPendingHostInteraction("req-1");

        Action act = () => session.RegisterPendingHostInteraction("req-1");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*req-1*");
    }

    [Fact]
    public void TryResolvePendingHostInteraction_WithPendingId_RemovesAndReturnsTrue()
    {
        var session = new ConversationSession(CreateContext());
        session.RegisterPendingHostInteraction("req-1");

        var resolved = session.TryResolvePendingHostInteraction("req-1");

        resolved.Should().BeTrue();
        session.IsHostInteractionPending("req-1").Should().BeFalse();
        session.PendingHostInteractionIds.Should().BeEmpty();
    }

    [Fact]
    public void TryResolvePendingHostInteraction_WithUnknownId_ReturnsFalse()
    {
        var session = new ConversationSession(CreateContext());

        session.TryResolvePendingHostInteraction("never-registered").Should().BeFalse();
    }

    [Fact]
    public void TryResolvePendingHostInteraction_CalledTwiceForSameId_SecondCallReturnsFalse()
    {
        var session = new ConversationSession(CreateContext());
        session.RegisterPendingHostInteraction("req-1");

        session.TryResolvePendingHostInteraction("req-1").Should().BeTrue();
        session.TryResolvePendingHostInteraction("req-1").Should().BeFalse();
    }

    [Fact]
    public void RegisterPendingHostInteraction_AfterResolve_AllowsReRegistration()
    {
        var session = new ConversationSession(CreateContext());
        session.RegisterPendingHostInteraction("req-1");
        session.TryResolvePendingHostInteraction("req-1");

        Action act = () => session.RegisterPendingHostInteraction("req-1");

        act.Should().NotThrow();
        session.IsHostInteractionPending("req-1").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolvePendingHostInteraction_WithInvalidId_ThrowsArgumentException(string? invalidId)
    {
        var session = new ConversationSession(CreateContext());

        Action act = () => session.TryResolvePendingHostInteraction(invalidId!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("requestId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsHostInteractionPending_WithInvalidId_ThrowsArgumentException(string? invalidId)
    {
        var session = new ConversationSession(CreateContext());

        Action act = () => session.IsHostInteractionPending(invalidId!);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("requestId");
    }

    [Fact]
    public void PendingHostInteractionIds_ReturnsSnapshotNotLiveView()
    {
        var session = new ConversationSession(CreateContext());
        session.RegisterPendingHostInteraction("req-1");

        var snapshot = session.PendingHostInteractionIds;
        session.RegisterPendingHostInteraction("req-2");

        snapshot.Should().ContainSingle().Which.Should().Be("req-1");
        session.PendingHostInteractionIds.Should().HaveCount(2);
    }

    // === Reset ===

    [Fact]
    public void Reset_ClearsActiveTopic()
    {
        var session = new ConversationSession(CreateContext());
        session.SetActiveTopic(CreateTopicDescriptor("topic-a"));

        session.Reset();

        session.ActiveTopic.Should().BeNull();
    }

    [Fact]
    public void Reset_ClearsPendingHostInteractions()
    {
        var session = new ConversationSession(CreateContext());
        session.RegisterPendingHostInteraction("req-1");

        session.Reset();

        session.PendingHostInteractionIds.Should().BeEmpty();
        session.IsHostInteractionPending("req-1").Should().BeFalse();
    }

    [Fact]
    public void Reset_ClearsHistoryCallStackAndSharedValues()
    {
        var session = new ConversationSession(CreateContext());
        session.SetActiveTopic(CreateTopicDescriptor("topic-a"));
        session.PushTopicCall("a", "b");
        session.SetValue("key", "value");

        session.Reset();

        session.TopicHistory.Should().BeEmpty();
        session.TopicCallDepth.Should().Be(0);
        session.HasValue("key").Should().BeFalse();
    }

    [Fact]
    public void Reset_PreservesConversationIdAndSubject()
    {
        var session = new ConversationSession(CreateContext(conversationId: "conv-1", userId: "user-1"));

        session.Reset();

        session.ConversationId.Should().Be("conv-1");
        session.Subject.Should().Be("user-1");
    }
}
