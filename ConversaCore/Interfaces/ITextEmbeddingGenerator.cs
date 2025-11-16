using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.Interfaces; 
/// <summary>
/// A minimal, stable abstraction for text embedding generation.
/// </summary>
public interface ITextEmbeddingGenerator {
    /// <summary>
    /// Generates an embedding vector from the given input text.
    /// </summary>
    /// <param name="text">Input text.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A float vector representing the embedding.</returns>
    Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}
