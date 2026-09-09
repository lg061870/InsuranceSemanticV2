using ConversaCore.Registration;
using ConversaCore.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace ConversaCore.Tests.Tools;

public sealed class ToolSelectorTests
{
    [Fact]
    public async Task Selector_RanksOnlyAllowlistedCandidatesWhenEnabled()
    {
        var first = Descriptor("first");
        var second = Descriptor("second");
        var ranker = new RecordingRanker { Scores = new Dictionary<string, float> { ["second"] = .9f } };
        var selector = new ToolSelector(new ToolCatalog(new[] { first, second }), ranker);

        var result = await selector.SelectAsync("find", new HashSet<string> { "second" }, new ToolSelectionOptions { Enabled = true });

        Assert.Equal("second", result!.Tool.ToolId);
        Assert.Equal(new[] { "second" }, ranker.Candidates);
    }

    [Fact]
    public async Task Selector_IsDisabledWithoutCallingRankerAndRejectsInvalidScores()
    {
        var ranker = new RecordingRanker { Scores = new Dictionary<string, float> { ["first"] = float.NaN } };
        var selector = new ToolSelector(new ToolCatalog(new[] { Descriptor("first") }), ranker);
        Assert.Null(await selector.SelectAsync("find", new HashSet<string> { "first" }, new ToolSelectionOptions()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => selector.SelectAsync(
            "find", new HashSet<string> { "first" }, new ToolSelectionOptions { Enabled = true }));
    }

    [Fact]
    public async Task Selector_AppliesCheapPrefilterAndTopKBeforeRanking()
    {
        var ranker = new RecordingRanker { Scores = new Dictionary<string, float> { ["alpha"] = .9f } };
        var selector = new ToolSelector(new ToolCatalog(new[]
        {
            new ToolDescriptor("alpha", "1", "Alpha", "alpha lookup", typeof(string), typeof(string)),
            new ToolDescriptor("beta", "1", "Beta", "beta lookup", typeof(string), typeof(string)),
            new ToolDescriptor("gamma", "1", "Gamma", "gamma lookup", typeof(string), typeof(string))
        }), ranker);

        await selector.SelectAsync("alpha", new HashSet<string> { "alpha", "beta", "gamma" },
            new ToolSelectionOptions { Enabled = true, MaxCandidates = 1 });

        Assert.Equal(["alpha"], ranker.Candidates);
    }

    private static ToolDescriptor Descriptor(string id) => new(id, "1", id, id, typeof(string), typeof(string));

    private sealed class RecordingRanker : IToolSemanticRanker
    {
        public IReadOnlyDictionary<string, float> Scores { get; init; } = new Dictionary<string, float>();
        public string[] Candidates { get; private set; } = [];
        public Task<IReadOnlyDictionary<string, float>> RankAsync(string request, IReadOnlyList<ToolDescriptor> candidates, CancellationToken cancellationToken = default)
        {
            Candidates = candidates.Select(c => c.ToolId).ToArray();
            return Task.FromResult(Scores);
        }
    }
}
