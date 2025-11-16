using ConversaCore.Interfaces;
using System.Text.Json;

namespace InsuranceAgent.DomainTypes;

/// <summary>
/// Aggregates several InsuranceRuleSet objects into one composite rule set
/// so a single SemanticQueryActivity can reason across all of them.
/// </summary>
public class CombinedInsuranceRuleSet : IDomainRuleSet {
    public List<InsuranceRuleSet> RuleSets { get; set; } = new();

    public CombinedInsuranceRuleSet() { }

    public CombinedInsuranceRuleSet(IEnumerable<InsuranceRuleSet> sets)
        => RuleSets.AddRange(sets);

    public string RuleSetId => $"combined-{Guid.NewGuid():N}";
    public string? Description => $"Combined rule set containing {RuleSets.Count} underwriting families.";

    public string ToJson() => JsonSerializer.Serialize(RuleSets, new JsonSerializerOptions {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    public Dictionary<string, object> GetMetadata() => new() {
        ["ruleSetCount"] = RuleSets.Count,
        ["includedProducts"] = RuleSets.Select(r => r.ProductCategory).Distinct().ToList()
    };

    public override string ToString() => $"CombinedInsuranceRuleSet({RuleSets.Count} sets)";
}
