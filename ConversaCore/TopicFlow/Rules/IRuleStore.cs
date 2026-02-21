using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.TopicFlow.Rules
{
    public interface IRuleStore
    {
        Task<IReadOnlyList<RuleCandidate>> RetrieveCandidatesAsync(
            string evidenceText,
            System.Collections.Generic.Dictionary<string, string>? metadataFilters = null,
            int topK = 12,
            CancellationToken ct = default);
    }
}
