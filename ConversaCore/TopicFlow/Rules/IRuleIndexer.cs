using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.TopicFlow.Rules
{
    public interface IRuleIndexer
    {
        /// <summary>
        /// Index rules from a JSON source file and emit canonical rule chunks for storage/retrieval.
        /// Implementations may write canonical chunks to a file, a vector DB, or another store.
        /// </summary>
        Task<int> IndexRulesAsync(string sourceFilePath, CancellationToken ct = default);
    }
}
