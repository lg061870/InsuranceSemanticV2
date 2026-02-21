using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConversaCore.TopicFlow.Rules;

namespace InsuranceAgent.Services
{
    /// <summary>
    /// In-memory sample rule store.
    /// Loads canonical JSONL created by RuleIndexerSample and performs a simple keyword match ranking.
    /// This is intentionally lightweight for developer testing; production should use embeddings + vector DB.
    /// </summary>
    public class RuleStoreSample : IRuleStore
    {
        private readonly List<CanonicalRule> _rules = new();

        public RuleStoreSample(string canonicalJsonlPath)
        {
            if (File.Exists(canonicalJsonlPath)) LoadFromFile(canonicalJsonlPath);
        }

        private void LoadFromFile(string path)
        {
            foreach (var line in File.ReadLines(path))
            {
                try
                {
                    var cr = JsonSerializer.Deserialize<CanonicalRule>(line);
                    if (cr != null) _rules.Add(cr);
                }
                catch
                {
                    // ignore invalid lines
                }
            }
        }

        public Task<IReadOnlyList<RuleCandidate>> RetrieveCandidatesAsync(string evidenceText, Dictionary<string, string>? metadataFilters = null, int topK = 12, CancellationToken ct = default)
        {
            metadataFilters ??= new Dictionary<string, string>();

            var tokens = (evidenceText ?? string.Empty).ToLowerInvariant().Split(new[] { ' ', '\n', '\r', '\t', ',', '.' }, StringSplitOptions.RemoveEmptyEntries);

            var candidates = new List<RuleCandidate>();

            foreach (var r in _rules)
            {
                if (ct.IsCancellationRequested) break;

                // metadata filter: carrier
                if (metadataFilters.TryGetValue("carrier", out var carrier) && !string.IsNullOrWhiteSpace(carrier))
                {
                    if (!string.Equals(r.Carrier, carrier, StringComparison.OrdinalIgnoreCase)) continue;
                }

                var hay = (r.RawText + " " + r.PredicateKey + " " + r.Summary).ToLowerInvariant();
                var score = tokens.Count(t => hay.Contains(t));

                if (score > 0)
                {
                    candidates.Add(new RuleCandidate
                    {
                        RuleId = r.RuleId,
                        ChunkId = r.ChunkId,
                        SourceFile = r.SourceFile,
                        Carrier = r.Carrier,
                        RawText = r.RawText,
                        Summary = r.Summary,
                        Score = score
                    });
                }
            }

            var top = candidates.OrderByDescending(c => c.Score).ThenBy(c => c.RuleId).Take(topK).ToArray();
            return Task.FromResult<IReadOnlyList<RuleCandidate>>(top);
        }
    }
}
