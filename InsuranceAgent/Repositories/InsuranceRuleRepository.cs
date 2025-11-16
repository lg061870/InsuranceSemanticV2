using ConversaCore.Interfaces;
using ConversaCore.Repositories;
using ConversaCore.Services;
using InsuranceAgent.DomainTypes;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace InsuranceAgent.Repositories;

/// <summary>
/// Loads insurance underwriting rule sets for Term Life, Whole Life,
/// and Final Expense products. Vector embeddings and prompt enrichment
/// are automatically handled by the base <see cref="DomainRuleRepository{TSet}"/>.
/// </summary>
public class InsuranceRuleRepository : DomainRuleRepository<InsuranceRuleSet> {
    private readonly JsonSerializerOptions _jsonOptions = new() {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public InsuranceRuleRepository(
        IVectorDatabaseService vectorDb,
        ILogger<InsuranceRuleRepository> logger)
        : base(vectorDb, logger) {
        LoadRuleSets();
    }

    // Collection name used in vector DB
    public override string CollectionName => "insurance-rules";

    // Domain-level rule sets
    public InsuranceRuleSet TermLifeRules { get; private set; } = new();
    public InsuranceRuleSet WholeLifeRules { get; private set; } = new();
    public InsuranceRuleSet FinalExpenseRules { get; private set; } = new();

    // ============================================================
    // LOADERS
    // ============================================================
    private void LoadRuleSets() {
        TermLifeRules = LoadRuleSetFromJson("TERM_LIFE_INS_RULES.json", "TermLife");
        WholeLifeRules = LoadRuleSetFromJson("WHOLE_LIFE_INS_RULES.json", "WholeLife");
        FinalExpenseRules = LoadRuleSetFromJson("FINAL_EXPENSE_INS_RULES.json", "FinalExpense");

        // Automatically initialize vector embeddings (fire-and-forget)
        _ = InitializeEmbeddingsAsync();
    }

    private InsuranceRuleSet LoadRuleSetFromJson(string fileName, string productType) {
        var path = Path.Combine(AppContext.BaseDirectory, "wwwroot", "json", fileName);
        if (!File.Exists(path)) {
            _logger.LogWarning("❌ File not found: {Path}", path);
            return new InsuranceRuleSet {
                Carrier = "N/A",
                CoverageType = productType,
                BenefitRange = "N/A"
            };
        }

        var json = File.ReadAllText(path);
        var parsedRules = JsonSerializer.Deserialize<List<InsuranceRuleSet>>(json, _jsonOptions) ?? new();

        // When JSON contains multiple rule entries, group them logically into one RuleSet
        return new InsuranceRuleSet {
            Carrier = "Multiple Carriers",
            CoverageType = productType,
            BenefitRange = "Varies",
            AdditionalFields = new Dictionary<string, object> {
                ["ruleCount"] = parsedRules.Count,
                ["sourceFile"] = fileName
            }
        };
    }

    // ============================================================
    // REPOSITORY INTERFACE IMPLEMENTATION
    // ============================================================
    public override IReadOnlyList<InsuranceRuleSet> GetAllRuleSets()
        => new List<InsuranceRuleSet> { TermLifeRules, WholeLifeRules, FinalExpenseRules };

    public override IReadOnlyList<InsuranceRuleSet> GetRulesByTag(string tag) {
        tag = tag.Trim().ToLowerInvariant();
        return tag switch {
            "term" or "term-life" => new List<InsuranceRuleSet> { TermLifeRules },
            "whole" or "whole-life" => new List<InsuranceRuleSet> { WholeLifeRules },
            "final" or "final-expense" => new List<InsuranceRuleSet> { FinalExpenseRules },
            _ => Array.Empty<InsuranceRuleSet>()
        };
    }
}
