using ConversaCore.Agentic;
using ConversaCore.Context;
using ConversaCore.Interfaces;
using ConversaCore.Models;
using ConversaCore.TopicFlow;
using ConversaCore.Topics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;
using Flow = ConversaCore.TopicFlow.TopicFlow;

namespace ConversaCore.Tests.Characterization;

/// <summary>
/// Proves the REAL async semantic-completion pipeline end to end.
///
/// <see cref="LegacyOrchestrationTests.AsyncCompletion_InsertsFollowupIntoActiveFlow_AndForwardsOriginalEvent"/>
/// only calls <c>DomainAgentService.HandleAsyncActivityCompleted</c> directly with a hand-built
/// <see cref="AsyncQueryCompletedEventArgs"/>. It never proves that a real activity, doing real
/// background work, raises that event on its own.
///
/// This test instead builds a real <see cref="SemanticQueryActivity{TRuleSet, TInput, TOutput}"/> —
/// the exact production activity type <c>MarketingT1Topic</c> uses for its background semantic
/// queries (see InsuranceAgent/Topics/MarketingTypeTopics/MarketingT1Topic.cs, e.g. the
/// "health_info_submitted" query) — with <c>RunInBackground = true</c>, drops it into a real
/// <see cref="ConversaCore.TopicFlow.TopicFlow"/>, and drives it through the real
/// <c>DomainAgentService.ProcessUserMessageAsync</c> entry point, exactly like a live host would.
///
/// The only substitution is <see cref="IChatCompletionService"/> — the interface Semantic Kernel
/// itself defines as the seam for swapping model backends. No real OpenAI/network call is made;
/// the fake implementation returns a canned response on an already-completed <see cref="Task"/>.
///
/// Observable pipeline exercised for real (no step is short-circuited or called directly):
/// activity.RunActivity (background Task.Run) -&gt; SemanticActivity.AsyncCompleted -&gt;
/// TopicFlow.Add's forwarding subscription -&gt; TopicFlow.AsyncActivityCompleted -&gt;
/// DomainAgentService.HandleAsyncActivityCompleted -&gt; flow.InsertNext(followup) -&gt;
/// DomainAgentService.AsyncActivityCompleted.
/// </summary>
public class LegacySemanticCompletionTests {
    [Fact]
    public async Task RealSemanticActivity_RunningInBackground_RaisesAsyncCompletion_ThroughRealPipeline() {
        // Arrange: a real Kernel wired with a deterministic fake chat completion
        // service in place of the real OpenAI connector. No network call ever happens.
        var fakeChat = new FakeChatCompletionService("{\"outputId\":\"async-result\"}");
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton<IChatCompletionService>(fakeChat);
        var kernel = kernelBuilder.Build();

        // The real production activity type (not a test double) used by
        // MarketingT1Topic for its background semantic queries.
        var semantic = new SemanticQueryActivity<FakeRuleSet, FakeInput, FakeOutput>(
            "semantic1", kernel, NullLogger.Instance,
            new FakeRuleSet(), () => new FakeInput(),
            outputGuidelinesPrompt: null, runInBackground: true);

        var followup = new SimpleActivity("followup", "result");
        semantic.OnAsyncCompleted(_ => Task.FromResult<TopicFlowActivity?>(followup));

        var flow = new LiveFlowProbe("SemanticFlow", 1f);
        flow.Add(semantic);

        var registry = new TopicRegistry();
        registry.RegisterTopic(flow);

        var agent = new AgentProbe(registry);

        var tcs = new TaskCompletionSource<AsyncQueryCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        agent.AsyncActivityCompleted += (_, e) => tcs.TrySetResult(e);

        // Act: drive the flow through the real message-processing entry point
        // (the same one a live host calls), not through any internal shortcut.
        await agent.Message("trigger the background semantic query");

        // The semantic activity's background Task.Run has not necessarily
        // finished when Message() returns (RunInBackground fires-and-forgets).
        // Wait on the real forwarded event instead of an arbitrary sleep.
        var forwarded = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert: the real activity code raised the completion end to end.
        Assert.Same(followup, forwarded.Activity);
        Assert.Contains(followup, flow.GetAllActivities());
        Assert.Equal(ActivityState.Created, followup.CurrentState);

        // And the real semantic pipeline actually executed (prompt building,
        // the fake model call, JSON parsing, context storage) rather than
        // being bypassed.
        Assert.Equal("{\"outputId\":\"async-result\"}", flow.Context.GetValue<string>("semantic1_Result"));
        var output = flow.Context.GetValue<FakeOutput>("output_query_semantic1");
        Assert.NotNull(output);
        Assert.Equal("async-result", output!.OutputId);
    }

    private sealed class AgentProbe(TopicRegistry registry) : DomainAgentService(
        registry, new ConversationContext("test", "user"), new TopicWorkflowContext(),
        NullLogger<DomainAgentService>.Instance) {
        public Task Message(string input) => ProcessUserMessageAsync(input);
    }

    /// <summary>
    /// Unlike the RoutingProbe helpers in LegacyOrchestrationTests, this flow does NOT
    /// override RunAsync/StepAsync/ProcessMessageAsync. It only fixes CanHandleAsync's
    /// confidence so routing is deterministic; execution goes through the real base
    /// ConversaCore.TopicFlow.TopicFlow queue-driven engine.
    /// </summary>
    private sealed class LiveFlowProbe(string name, float confidence)
        : Flow(new TopicWorkflowContext(), NullLogger.Instance, name) {
        public override Task<float> CanHandleAsync(string message, CancellationToken cancellationToken = default)
            => Task.FromResult(confidence);
    }

    /// <summary>
    /// Fakes the one seam Semantic Kernel itself provides for swapping model
    /// backends. Returns an already-completed Task; no network I/O.
    /// </summary>
    private sealed class FakeChatCompletionService(string responseText) : IChatCompletionService {
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default) {
            IReadOnlyList<ChatMessageContent> result = new List<ChatMessageContent> {
                new(AuthorRole.Assistant, responseText)
            };
            return Task.FromResult(result);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Streaming is not exercised by this characterization test.");
    }

    private sealed class FakeRuleSet : IDomainRuleSet {
        public string RuleSetId => "characterization-rules";
        public string? Description => "Deterministic rule set for characterization testing.";
        public string ToJson() => "{}";
        public Dictionary<string, object>? GetMetadata() => null;
    }

    private sealed class FakeInput {
        public string Value { get; set; } = "input";
    }

    private sealed class FakeOutput : IDomainOutput {
        public string OutputId { get; set; } = string.Empty;
        public string ToJson() => JsonSerializer.Serialize(this);
    }
}
