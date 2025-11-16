namespace ConversaCore.Interfaces;

/// <summary>
/// Contract for domain rule repositories that provide structured rule sets
/// consumable by semantic reasoning activities and vector services.
/// </summary>
/// <typeparam name="TRuleSet">A domain-defined type implementing <see cref="IDomainRuleSet"/>.</typeparam>
public interface IRuleRepository<TRuleSet>
    where TRuleSet : IDomainRuleSet {
    /// <summary>
    /// Returns all available rule sets (e.g., Term Life, Whole Life, Final Expense, etc.).
    /// </summary>
    IReadOnlyList<TRuleSet> GetAllRuleSets();

    /// <summary>
    /// Retrieves one or more rule sets by tag, type, or category.
    /// </summary>
    IReadOnlyList<TRuleSet> GetRulesByTag(string tag);

    /// <summary>
    /// Provides a canonical collection name for vector database storage.
    /// </summary>
    string CollectionName { get; }

    /// <summary>
    /// Optional short description for debugging, logging, or diagnostics.
    /// </summary>
    string? Description { get; }

    // ==========================================================
    // 🧠 Framework-managed extensions
    // ==========================================================

    /// <summary>
    /// Initializes vector embeddings for all available rule sets.
    /// Called automatically by <see cref="DomainRuleRepository{TRuleSet}"/>.
    /// </summary>
    Task InitializeEmbeddingsAsync();

    /// <summary>
    /// Generates a system prompt enriched with relevant context
    /// from the vector database (if available).
    /// </summary>
    /// <param name="userPrompt">The user or activity-level query prompt.</param>
    /// <returns>Full system prompt enriched with retrieved rule content.</returns>
    Task<string> GenerateSystemPromptAsync(string userPrompt);
}
