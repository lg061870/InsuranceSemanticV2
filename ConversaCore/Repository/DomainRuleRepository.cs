using System.Text.Json;
using ConversaCore.Interfaces;
using ConversaCore.Services;
using Microsoft.Extensions.Logging;

namespace ConversaCore.Repositories;

/// <summary>
/// Base repository that automatically handles rule embeddings and
/// semantic prompt enrichment for any domain rule set.
/// </summary>
/// <typeparam name="TSet">Domain rule set type implementing <see cref="IDomainRuleSet"/>.</typeparam>
public abstract class DomainRuleRepository<TSet> : IRuleRepository<TSet>
    where TSet : IDomainRuleSet {
    protected readonly IVectorDatabaseService _vectorDb;
    protected readonly ILogger _logger;

    protected DomainRuleRepository(IVectorDatabaseService vectorDb, ILogger logger) {
        _vectorDb = vectorDb ?? throw new ArgumentNullException(nameof(vectorDb));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ============================================================
    // ABSTRACT MEMBERS — must be defined by subclasses
    // ============================================================
    public abstract string CollectionName { get; }
    public virtual string? Description => null;

    public abstract IReadOnlyList<TSet> GetAllRuleSets();
    public abstract IReadOnlyList<TSet> GetRulesByTag(string tag);

    // ============================================================
    // PUBLIC INTERFACE IMPLEMENTATIONS
    // ============================================================

    /// <summary>
    /// Initializes vector embeddings for all available rule sets.
    /// </summary>
    public async Task InitializeEmbeddingsAsync() => await InitializeEmbeddingsInternalAsync();

    /// <summary>
    /// Generates an AI system prompt enriched with context from the vector DB.
    /// </summary>
    public async Task<string> GenerateSystemPromptAsync(string userPrompt)
        => await GenerateSystemPromptInternalAsync(userPrompt);

    // ============================================================
    // NEW FEATURE: SEMANTIC RETRIEVAL
    // ============================================================

    /// <summary>
    /// Retrieves the most relevant rule sets from the vector database
    /// based on a natural-language query (e.g., derived from LeadSummary).
    /// </summary>
    /// <param name="query">Natural language query describing the current case or lead.</param>
    /// <param name="topK">Maximum number of results to return.</param>
    /// <returns>List of deserialized rule sets relevant to the query.</returns>
    public async Task<IReadOnlyList<TSet>> SearchRelevantRuleSetsAsync(string query, int topK = 5) {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query text cannot be null or empty.", nameof(query));

        _logger.LogInformation("[{Repo}] 🔍 Performing semantic rule search for: {Query}", GetType().Name, query);

        try {
            // 1️⃣ Perform semantic search — embeddings handled internally
            var results = await _vectorDb.SearchAsync(CollectionName, query, limit: topK);

            if (results.Count == 0) {
                _logger.LogWarning("[{Repo}] ⚠️ No relevant rule sets found for query: {Query}", GetType().Name, query);
                return Array.Empty<TSet>();
            }

            // 2️⃣ Deserialize into rule sets
            var ruleSets = results
                .Select(hit => JsonSerializer.Deserialize<TSet>(hit.Content))
                .Where(x => x != null)
                .ToList()!;

            _logger.LogInformation("[{Repo}] ✅ Retrieved {Count} relevant rule sets for query: {Query}",
                GetType().Name, ruleSets.Count, query);

            return ruleSets;
        } catch (Exception ex) {
            _logger.LogError(ex, "[{Repo}] ❌ Failed semantic search for query: {Query}", GetType().Name, query);
            return Array.Empty<TSet>();
        }
    }

    // ============================================================
    // PROTECTED INTERNAL IMPLEMENTATION
    // ============================================================

    /// <summary>
    /// Protected internal logic for embedding rule sets into the vector database.
    /// Subclasses can override if they want finer-grained control.
    /// </summary>
    protected virtual async Task InitializeEmbeddingsInternalAsync() {
        try {
            var ruleSets = GetAllRuleSets();
            foreach (var ruleSet in ruleSets) {
                var metadata = ruleSet.GetMetadata() ?? new();
                var content = ruleSet.ToJson();

                await _vectorDb.StoreDocumentAsync(
                    collectionName: CollectionName,
                    documentId: ruleSet.RuleSetId,
                    content: content,
                    metadata: metadata
                );
            }

            _logger.LogInformation("[{Repo}] ✅ Initialized embeddings for {Count} rule sets.",
                GetType().Name, ruleSets.Count);
        } catch (Exception ex) {
            _logger.LogError(ex, "[{Repo}] ❌ Failed to initialize rule embeddings.", GetType().Name);
        }
    }

    /// <summary>
    /// Protected internal logic for composing a system prompt using vector retrieval.
    /// Subclasses may override to customize prompt formatting.
    /// </summary>
    protected virtual async Task<string> GenerateSystemPromptInternalAsync(string userPrompt) {
        try {
            // 1️⃣ Ensure embeddings exist
            var collections = await _vectorDb.GetCollectionsAsync();
            if (!collections.Contains(CollectionName)) {
                _logger.LogWarning("[{Repo}] ⚠️ Vector collection '{Collection}' not found. Initializing...",
                    GetType().Name, CollectionName);
                await InitializeEmbeddingsInternalAsync();
            }

            // 2️⃣ Semantic search for relevant rule sets
            var results = await _vectorDb.SearchAsync(CollectionName, userPrompt, limit: 3);
            if (results.Count == 0) {
                _logger.LogWarning("[{Repo}] ⚠️ No matching rule embeddings found for '{Prompt}'.",
                    GetType().Name, userPrompt);
                return userPrompt;
            }

            // 3️⃣ Build enriched prompt
            var combined = string.Join("\n\n---\n\n", results.Select(r => r.Content));

            var enrichedPrompt =
$@"You are an expert reasoning engine.
The following contextual rule sets were retrieved from '{CollectionName}':

{combined}

Now process the following instruction:
{userPrompt}";

            return enrichedPrompt;
        } catch (Exception ex) {
            _logger.LogError(ex, "[{Repo}] ❌ Failed to generate enriched system prompt.");
            return userPrompt;
        }
    }
}
