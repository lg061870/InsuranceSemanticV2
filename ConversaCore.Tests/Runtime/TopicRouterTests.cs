using ConversaCore.Registration;
using ConversaCore.Runtime;
using ConversaCore.Topics;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Tests.Runtime;

public class TopicRouterTests
{
    // Every descriptor is a tripwire: routing must never activate or deliver to a topic.
    private static TopicDescriptor Topic(string id, int priority = 0,
        TopicClassification classification = TopicClassification.Domain,
        TopicInterruptionPolicy policy = TopicInterruptionPolicy.FirstRefusal,
        params string[] phrases) => new(id, _ => throw new InvalidOperationException("Factory invoked"))
        {
            Priority = priority,
            Classification = classification,
            InterruptionPolicy = policy,
            TriggerPhrases = phrases.ToHashSet()
        };

    private static TopicRouter Router(IEnumerable<TopicDescriptor> topics,
        ITopicSemanticRanker? ranker = null, TopicRouterOptions? options = null) =>
        new(new TopicCatalog(topics), options, ranker);

    private sealed class Ranker(Func<string, IReadOnlyList<TopicDescriptor>, CancellationToken,
        Task<IReadOnlyDictionary<string, float>>> rank) : ITopicSemanticRanker
    {
        public Task<IReadOnlyDictionary<string, float>> RankAsync(string message,
            IReadOnlyList<TopicDescriptor> candidates, CancellationToken cancellationToken = default) =>
            rank(message, candidates, cancellationToken);
    }

    private static Ranker NeverRank() => new((_, _, _) => throw new InvalidOperationException("Unexpected ranking"));
    private static Ranker Scores(params (string Id, float Score)[] scores) =>
        new((_, _, _) => Task.FromResult<IReadOnlyDictionary<string, float>>(
            scores.ToDictionary(s => s.Id, s => s.Score, StringComparer.Ordinal)));

    private static void Decision(TopicRoutingDecision actual, TopicRoutingDecisionKind kind,
        TopicDescriptor? topic, float? confidence = null)
    {
        Assert.Equal(kind, actual.Kind);
        Assert.Same(topic, actual.Topic);
        Assert.Equal(confidence, actual.Confidence);
    }

    [Theory]
    [InlineData(TopicClassification.Domain)]
    [InlineData(TopicClassification.System)]
    public async Task Waiting_first_refusal_precedes_competing_exact_match(TopicClassification classification)
    {
        var active = Topic("active", classification: classification);
        var router = Router([active, Topic("other", 100)], NeverRank());
        Decision(await router.RouteAsync(new("other", "ACTIVE", ActiveTopicInputState.Waiting)),
            TopicRoutingDecisionKind.OfferToActive, active);
    }

    [Theory]
    [InlineData(TopicInterruptionPolicy.FirstRefusal)]
    [InlineData(TopicInterruptionPolicy.Interruptible)]
    public async Task Accepted_input_is_already_handled_without_redelivery(TopicInterruptionPolicy policy)
    {
        var active = Topic("active", policy: policy);
        Decision(await Router([active, Topic("other")], NeverRank()).RouteAsync(
            new("other", "active", ActiveTopicInputState.Accepted)), TopicRoutingDecisionKind.AlreadyHandled, active);
    }

    [Fact]
    public async Task Declined_active_is_excluded_from_exact_and_semantic_candidates()
    {
        var active = Topic("active", 100);
        var other = Topic("other");
        var ranker = new Ranker((message, candidates, _) =>
        {
            Assert.Equal("active", message);
            Assert.Same(other, Assert.Single(candidates));
            return Task.FromResult<IReadOnlyDictionary<string, float>>(new Dictionary<string, float> { ["other"] = .8f });
        });
        Decision(await Router([active, other], ranker).RouteAsync(new("active", "ACTIVE", ActiveTopicInputState.Declined)),
            TopicRoutingDecisionKind.SemanticMatch, other, .8f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Interruptible_waiting_allows_a_qualifying_match(bool semantic)
    {
        var active = Topic("active", policy: TopicInterruptionPolicy.Interruptible);
        var other = Topic("other");
        Decision(await Router([active, other], semantic ? Scores(("other", .8f)) : NeverRank())
            .RouteAsync(new(semantic ? "unmatched" : "other", "active", ActiveTopicInputState.Waiting)),
            semantic ? TopicRoutingDecisionKind.SemanticMatch : TopicRoutingDecisionKind.DeterministicMatch,
            other, semantic ? .8f : 1f);
    }

    [Fact]
    public async Task Interruptible_waiting_without_match_is_offered_input_before_fallback()
    {
        var active = Topic("active", policy: TopicInterruptionPolicy.Interruptible);
        var fallback = Topic("fallback", classification: TopicClassification.System);
        Decision(await Router([active, fallback], Scores(("active", .2f)),
            new() { FallbackTopicId = "fallback" }).RouteAsync(new("unmatched", "active", ActiveTopicInputState.Waiting)),
            TopicRoutingDecisionKind.OfferToActive, active);
    }

    [Theory]
    [InlineData("  POLICY.QUOTE  ")]
    [InlineData("  GET a QUOTE \t")]
    public async Task Exact_id_and_phrase_are_trimmed_case_insensitive_and_skip_ranking(string message)
    {
        var topic = Topic("policy.quote", phrases: ["get a quote"]);
        Decision(await Router([topic], NeverRank(), new() { MinimumConfidence = 1 }).RouteAsync(new(message)),
            TopicRoutingDecisionKind.DeterministicMatch, topic, 1f);
    }

    [Theory]
    [InlineData("quote")]
    [InlineData("please get a quote")]
    [InlineData("Display title")]
    [InlineData("descriptive text")]
    public async Task Deterministic_matching_does_not_use_substrings_display_names_or_description(string message)
    {
        var topic = Topic("policy.quote", phrases: ["get a quote"]) with
        { DisplayName = "Display title", Description = "descriptive text" };
        Decision(await Router([topic]).RouteAsync(new(message)), TopicRoutingDecisionKind.NoMatch, null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Priority_then_ordinal_case_insensitive_id_break_ties_independent_of_registration_order(bool semantic)
    {
        var winner = Topic("alpha", 10, phrases: ["shared"]);
        var topics = new[] { Topic("Zebra", 10, phrases: ["shared"]), Topic("aardvark", 1, phrases: ["shared"]), winner };
        foreach (var ordering in new[] { topics, topics.Reverse().ToArray(), new[] { topics[1], topics[2], topics[0] } })
        {
            Decision(await Router(ordering, semantic ? Scores(("Zebra", .7f), ("ALPHA", .7f), ("aardvark", .7f)) : NeverRank())
                .RouteAsync(new(semantic ? "unmatched" : "shared")),
                semantic ? TopicRoutingDecisionKind.SemanticMatch : TopicRoutingDecisionKind.DeterministicMatch,
                winner, semantic ? .7f : 1f);
        }
    }

    [Fact]
    public async Task Semantic_candidates_are_bounded_sorted_and_exclude_system_topics()
    {
        var a = Topic("alpha", 5);
        var z = Topic("Zebra", 5);
        var ranker = new Ranker((_, candidates, _) =>
        {
            Assert.Equal(new[] { a, z }, candidates);
            return Task.FromResult<IReadOnlyDictionary<string, float>>(new Dictionary<string, float>());
        });
        Decision(await Router([Topic("system", 100, TopicClassification.System), z, Topic("low"), a], ranker,
            new() { MaxSemanticCandidates = 2 }).RouteAsync(new("unmatched")), TopicRoutingDecisionKind.NoMatch, null);
    }

    [Fact]
    public async Task Semantic_bound_does_not_limit_deterministic_matching()
    {
        var low = Topic("low");
        Decision(await Router([Topic("high", 100), low], NeverRank(), new() { MaxSemanticCandidates = 1 })
            .RouteAsync(new("low")), TopicRoutingDecisionKind.DeterministicMatch, low, 1f);
    }

    [Theory]
    [InlineData("system")]
    [InlineData("system phrase")]
    public async Task System_topics_are_not_deterministic_candidates(string input)
    {
        Decision(await Router([Topic("system", classification: TopicClassification.System, phrases: ["system phrase"])], NeverRank())
            .RouteAsync(new(input)), TopicRoutingDecisionKind.NoMatch, null);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task Blank_input_skips_semantic_ranking(string input) =>
        Decision(await Router([Topic("domain")], NeverRank()).RouteAsync(new(input)), TopicRoutingDecisionKind.NoMatch, null);

    [Fact]
    public async Task Empty_or_fully_declined_catalog_skips_semantic_ranking()
    {
        Decision(await Router([], NeverRank()).RouteAsync(new("unmatched")), TopicRoutingDecisionKind.NoMatch, null);
        Decision(await Router([Topic("active")], NeverRank()).RouteAsync(new("active", "active", ActiveTopicInputState.Declined)),
            TopicRoutingDecisionKind.NoMatch, null);
    }

    [Theory]
    [InlineData(.49f, TopicRoutingDecisionKind.NoMatch)]
    [InlineData(.5f, TopicRoutingDecisionKind.SemanticMatch)]
    [InlineData(1f, TopicRoutingDecisionKind.SemanticMatch)]
    [InlineData(0f, TopicRoutingDecisionKind.NoMatch)]
    public async Task Confidence_threshold_is_inclusive(float score, TopicRoutingDecisionKind kind)
    {
        var topic = Topic("domain");
        Decision(await Router([topic], Scores(("DOMAIN", score))).RouteAsync(new("unmatched")), kind,
            kind == TopicRoutingDecisionKind.NoMatch ? null : topic, kind == TopicRoutingDecisionKind.NoMatch ? null : score);
    }

    [Fact]
    public async Task Higher_semantic_score_wins_before_priority_and_missing_scores_are_not_matches()
    {
        var winner = Topic("winner", -10);
        Decision(await Router([Topic("unscored", 1000), Topic("priority", 100), winner],
            Scores(("priority", .8f), ("winner", .9f))).RouteAsync(new("unmatched")),
            TopicRoutingDecisionKind.SemanticMatch, winner, .9f);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-.01f)]
    [InlineData(1.01f)]
    public async Task Invalid_semantic_score_is_rejected_even_with_another_valid_winner(float score) =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Router([Topic("a"), Topic("b")],
            Scores(("a", 1), ("b", score))).RouteAsync(new("unmatched")));

    [Theory]
    [InlineData("unknown")]
    [InlineData("system")]
    [InlineData("outside-bound")]
    [InlineData("")]
    public async Task Scores_for_ineligible_ids_are_rejected(string id) =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Router(
            [Topic("eligible", 100), Topic("outside-bound"), Topic("system", classification: TopicClassification.System)],
            Scores(("eligible", 1), (id, .1f)), new() { MaxSemanticCandidates = 1 }).RouteAsync(new("unmatched")));

    [Fact]
    public async Task Ranker_cannot_return_declined_topic() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Router([Topic("active"), Topic("other")], Scores(("active", 1)))
            .RouteAsync(new("unmatched", "active", ActiveTopicInputState.Declined)));

    [Fact]
    public async Task Case_insensitive_duplicate_score_ids_are_rejected() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Router([Topic("a")], Scores(("a", .7f), ("A", .8f)))
            .RouteAsync(new("unmatched")));

    [Fact]
    public async Task Null_scores_are_rejected() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => Router([Topic("a")],
            new Ranker((_, _, _) => Task.FromResult<IReadOnlyDictionary<string, float>>(null!))).RouteAsync(new("unmatched")));

    [Fact]
    public async Task Scorer_error_propagates_without_fallback()
    {
        var failure = new ApplicationException("Scorer failed");
        var router = Router([Topic("a"), Topic("fallback", classification: TopicClassification.System)],
            new Ranker((_, _, _) => Task.FromException<IReadOnlyDictionary<string, float>>(failure)),
            new() { FallbackTopicId = "fallback" });
        Assert.Same(failure, await Assert.ThrowsAsync<ApplicationException>(() => router.RouteAsync(new("unmatched"))));
    }

    [Theory]
    [InlineData(ActiveTopicInputState.None)]
    [InlineData(ActiveTopicInputState.Waiting)]
    [InlineData(ActiveTopicInputState.Accepted)]
    public async Task Precancelled_request_propagates_before_any_decision(ActiveTopicInputState state)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Router([Topic("a")], NeverRank())
            .RouteAsync(new("a", "a", state), cts.Token));
        Assert.Equal(cts.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_during_ranking_propagates_even_if_scorer_returns_scores(bool scorerThrows)
    {
        using var cts = new CancellationTokenSource();
        var ranker = new Ranker((message, _, token) =>
        {
            Assert.Equal("  original input  ", message);
            Assert.Equal(cts.Token, token);
            cts.Cancel();
            if (scorerThrows) token.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyDictionary<string, float>>(new Dictionary<string, float> { ["a"] = 1 });
        });
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Router([Topic("a")], ranker)
            .RouteAsync(new("  original input  "), cts.Token));
        Assert.Equal(cts.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("domain")]
    [InlineData("")]
    [InlineData(" ")]
    public void Fallback_must_be_a_registered_system_topic(string id) =>
        Assert.Throws<ArgumentException>(() => Router([Topic("domain")], options: new() { FallbackTopicId = id }));

    [Fact]
    public async Task Fallback_is_case_insensitive_and_only_used_after_no_qualifying_match()
    {
        var fallback = Topic("fallback", classification: TopicClassification.System);
        var domain = Topic("domain");
        var router = Router([domain, fallback], Scores(("domain", .1f)), new() { FallbackTopicId = "FALLBACK" });
        Decision(await router.RouteAsync(new("unmatched")), TopicRoutingDecisionKind.Fallback, fallback);
        Decision(await router.RouteAsync(new("domain")), TopicRoutingDecisionKind.DeterministicMatch, domain, 1);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(1.01f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Invalid_threshold_is_rejected(float threshold) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Router([], options: new() { MinimumConfidence = threshold }));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nonpositive_candidate_bound_is_rejected(int bound) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Router([], options: new() { MaxSemanticCandidates = bound }));

    [Fact]
    public async Task Null_catalog_request_and_message_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new TopicRouter(null!));
        var router = Router([]);
        await Assert.ThrowsAsync<ArgumentNullException>(() => router.RouteAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => router.RouteAsync(new(null!)));
    }

    [Theory]
    [InlineData(null, ActiveTopicInputState.Waiting)]
    [InlineData(null, ActiveTopicInputState.Accepted)]
    [InlineData(null, ActiveTopicInputState.Declined)]
    [InlineData("missing", ActiveTopicInputState.None)]
    [InlineData("missing", ActiveTopicInputState.Waiting)]
    public async Task Invalid_active_topic_state_is_rejected(string? id, ActiveTopicInputState state) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Router([Topic("a")], NeverRank()).RouteAsync(new("a", id, state)));

    [Fact]
    public async Task Undefined_active_state_is_rejected() =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Router([Topic("a")], NeverRank())
            .RouteAsync(new("a", "a", (ActiveTopicInputState)999)));

    [Fact]
    public void Trigger_phrases_are_copied_trimmed_deduplicated_and_case_insensitive()
    {
        var source = new HashSet<string> { "  Get Quote  ", "get quote" };
        var descriptor = Topic("a") with { TriggerPhrases = source };
        source.Clear();
        source.Add("replacement");
        Assert.Equal("Get Quote", Assert.Single(descriptor.TriggerPhrases));
        Assert.Contains("GET QUOTE", descriptor.TriggerPhrases);
        Assert.DoesNotContain("replacement", descriptor.TriggerPhrases);
        if (descriptor.TriggerPhrases is ISet<string> mutable)
            Assert.Throws<NotSupportedException>(() => mutable.Add("injected"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void Invalid_trigger_phrase_is_rejected(string? phrase) =>
        Assert.Throws<ArgumentException>(() => Topic("a") with { TriggerPhrases = new HashSet<string> { phrase! } });

    [Fact]
    public void Null_trigger_phrase_collection_is_rejected() =>
        Assert.Throws<ArgumentNullException>(() => Topic("a") with { TriggerPhrases = null! });

    [Fact]
    public async Task Builder_phrase_configuration_is_copied_and_used_without_activation()
    {
        var services = new ServiceCollection();
        var phrases = new HashSet<string> { "  Get Quote  " };
        services.AddConversaCoreBuilder("test-key").AddTopic<ITopic>("quote", _ => throw new InvalidOperationException("Factory invoked"),
            options => options.TriggerPhrases = phrases);
        phrases.Clear();
        using var provider = services.BuildServiceProvider();
        var descriptor = Assert.Single(provider.GetServices<TopicDescriptor>());
        Decision(await Router([descriptor], NeverRank()).RouteAsync(new("GET QUOTE")),
            TopicRoutingDecisionKind.DeterministicMatch, descriptor, 1);
    }

    [Fact]
    public async Task Concurrent_requests_keep_declined_candidates_and_results_independent()
    {
        var a = Topic("a");
        var b = Topic("b");
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var ranker = new Ranker(async (message, candidates, _) =>
        {
            var expected = message == "first" ? b : a;
            Assert.Same(expected, Assert.Single(candidates));
            if (Interlocked.Increment(ref count) == 2) bothEntered.SetResult();
            await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Same(expected, Assert.Single(candidates));
            return new Dictionary<string, float> { [expected.TopicId] = .9f };
        });
        var router = Router([a, b], ranker);
        var results = await Task.WhenAll(
            router.RouteAsync(new("first", "a", ActiveTopicInputState.Declined)),
            router.RouteAsync(new("second", "b", ActiveTopicInputState.Declined)));
        Decision(results[0], TopicRoutingDecisionKind.SemanticMatch, b, .9f);
        Decision(results[1], TopicRoutingDecisionKind.SemanticMatch, a, .9f);
        Decision(await router.RouteAsync(new("a")), TopicRoutingDecisionKind.DeterministicMatch, a, 1);
    }
}
