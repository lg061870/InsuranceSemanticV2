using ConversaCore.TopicFlow;
using InsuranceAgent.Repositories;

namespace InsuranceAgent.DomainTypes;

/// <summary>
/// Selects relevant insurance rule sets based on user intents
/// and optionally performs semantic retrieval via the vector database.
/// Returns a CombinedInsuranceRuleSet for unified reasoning.
/// </summary>
public static class RuleSelector {
    /// <summary>
    /// Selects and merges one or more <see cref="InsuranceRuleSet"/> instances
    /// into a single <see cref="CombinedInsuranceRuleSet"/> depending on
    /// user intent flags and semantic similarity to the lead summary.
    /// </summary>
    /// <param name="ctx">The active TopicWorkflowContext containing user goals and answers.</param>
    /// <param name="repo">The rule repository providing domain rule sets.</param>
    /// <param name="useVectorRetrieval">
    /// If true, performs a semantic search for the most relevant rule sets using the vector database.
    /// </param>
    public static async Task<CombinedInsuranceRuleSet> SelectRulesAsync(
        TopicWorkflowContext ctx,
        InsuranceRuleRepository repo,
        bool useVectorRetrieval = true) {
        if (ctx == null)
            throw new ArgumentNullException(nameof(ctx));
        if (repo == null)
            throw new ArgumentNullException(nameof(repo));

        // Retrieve user intents (AdaptiveCard booleans)
        bool protectLovedOnes = ctx.GetValue<bool>("intent_protect_loved_ones");
        bool payMortgage = ctx.GetValue<bool>("intent_pay_mortgage");
        bool prepareFuture = ctx.GetValue<bool>("intent_prepare_future");
        bool peaceOfMind = ctx.GetValue<bool>("intent_peace_of_mind");
        bool coverExpenses = ctx.GetValue<bool>("intent_cover_expenses");
        bool unsure = ctx.GetValue<bool>("intent_unsure");

        var selected = new List<InsuranceRuleSet>();

        try {
            // ===============================================================
            // 🧠 VECTOR RETRIEVAL PATH (semantic search for matching rule sets)
            // ===============================================================
            if (useVectorRetrieval) {
                // Build a natural-language search query based on context
                string query = BuildSemanticQuery(ctx);

                var relevant = await repo.SearchRelevantRuleSetsAsync(query, topK: 5);

                if (relevant != null && relevant.Count > 0) {
                    selected.AddRange(relevant);
                    ctx.SetValue("ruleSelector_method", "vector_search");
                }
            }

            // ===============================================================
            // 🧩 FALLBACK: INTENT-BASED SELECTION (if no semantic results)
            // ===============================================================
            if (selected.Count == 0) {
                ctx.SetValue("ruleSelector_method", "intent_fallback");

                // === TERM LIFE ===
                if (protectLovedOnes || payMortgage || coverExpenses)
                    selected.AddRange(repo.GetRulesByTag("TermLife"));

                // === WHOLE LIFE ===
                if (prepareFuture || peaceOfMind)
                    selected.AddRange(repo.GetRulesByTag("WholeLife"));

                // === UNSURE OR MIXED ===
                if (unsure || selected.Count == 0)
                    selected.AddRange(repo.GetAllRuleSets());
            }

            // ===============================================================
            // 🧾 DEDUPLICATION & COMBINATION
            // ===============================================================
            var distinct = selected
                .GroupBy(r => r.RuleSetId)
                .Select(g => g.First())
                .ToList();

            var combined = new CombinedInsuranceRuleSet(distinct);

            ctx.SetValue("active_rule_set_count", distinct.Count);
            return combined;
        } catch (Exception ex) {
            ctx.SetValue("ruleSelector_error", ex.Message);
            return new CombinedInsuranceRuleSet(new List<InsuranceRuleSet>());
        }
    }

    // ===============================================================
    // INTERNAL HELPERS
    // ===============================================================

    /// <summary>
    /// Builds a natural-language query string from the active TopicWorkflowContext.
    /// Used when performing semantic retrieval.
    /// </summary>
    private static string BuildSemanticQuery(TopicWorkflowContext ctx) {
        var parts = new List<string>();

        if (ctx.ContainsKey("intent_protect_loved_ones")) parts.Add("protect loved ones");
        if (ctx.ContainsKey("intent_pay_mortgage")) parts.Add("pay mortgage balance");
        if (ctx.ContainsKey("intent_prepare_future")) parts.Add("prepare for family future");
        if (ctx.ContainsKey("intent_peace_of_mind")) parts.Add("peace of mind insurance");
        if (ctx.ContainsKey("intent_cover_expenses")) parts.Add("cover final expenses");
        if (ctx.ContainsKey("intent_unsure")) parts.Add("unsure about coverage type");

        // Optional: include lead summary or health notes if available
        string? summary = ctx.GetValue<string>("lead_summary_text");
        if (!string.IsNullOrWhiteSpace(summary))
            parts.Add(summary);

        return string.Join(", ", parts);
    }
}
