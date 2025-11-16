using ConversaCore.Interfaces;
using Microsoft.SemanticKernel.Embeddings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConversaCore.Services {
    /// <summary>
    /// Adapter to bridge Semantic Kernel's ITextEmbeddingGenerationService
    /// to ConversaCore's ITextEmbeddingGenerator abstraction.
    /// </summary>
    public class SemanticKernelEmbeddingAdapter : ITextEmbeddingGenerator {
        private readonly ITextEmbeddingGenerationService _semanticService;

        public SemanticKernelEmbeddingAdapter(ITextEmbeddingGenerationService semanticService) {
            _semanticService = semanticService
                ?? throw new ArgumentNullException(nameof(semanticService));
        }

        public async Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
            string text,
            CancellationToken cancellationToken = default) {
            // SK returns ReadOnlyMemory<float> now — no .Vector property
            ReadOnlyMemory<float> embedding =
                await _semanticService.GenerateEmbeddingAsync(
                    text,
                    cancellationToken: cancellationToken);

            // Convert to a regular list for easier downstream processing
            return embedding.ToArray();
        }
    }
}
