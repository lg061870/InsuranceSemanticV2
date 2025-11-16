using System.Text.Json;
using System.Text.Json.Serialization;
using ConversaCore.Interfaces;

namespace InsuranceAgent.DomainTypes;

/// <summary>
/// Represents a logical underwriting rule set for a carrier and product type.
/// Implements <see cref="IDomainRuleSet"/> so that it can be serialized,
/// embedded, and reasoned upon by the semantic engine.
/// </summary>
public class InsuranceRuleSet : IDomainRuleSet {
    // ================================================================
    // === Identification and Metadata ===
    // ================================================================
    [JsonPropertyName("RuleSetId")]
    public string RuleSetId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("Carrier")]
    public string Carrier { get; set; } = string.Empty;

    [JsonPropertyName("COVERAGE_TYPE")]
    public string? CoverageType { get; set; }

    [JsonPropertyName("BENEFIT_RANGE")]
    public string? BenefitRange { get; set; }

    [JsonPropertyName("AGE_RANGE")]
    public string? AgeRange { get; set; }

    [JsonPropertyName("BUILD__UW_GUIDE")]
    public string? BuildGuide { get; set; }

    [JsonPropertyName("PAYMENT_METHODS")]
    public string? PaymentMethods { get; set; }

    // ================================================================
    // === Medical / Underwriting Categories ===
    // ================================================================
    [JsonPropertyName("CANCER__IMMUNE_DISORDERS")]
    public Dictionary<string, string> CancerImmuneDisorders { get; set; } = new();

    [JsonPropertyName("CARDIOVASCULAR")]
    public Dictionary<string, string> Cardiovascular { get; set; } = new();

    [JsonPropertyName("NEUROLOGICAL__COGNITIVE")]
    public Dictionary<string, string> NeurologicalCognitive { get; set; } = new();

    [JsonPropertyName("MENTAL_HEALTH__SUBSTANCE_USE")]
    public Dictionary<string, string> MentalHealthSubstanceUse { get; set; } = new();

    [JsonPropertyName("RESPIRATORY")]
    public Dictionary<string, string> Respiratory { get; set; } = new();

    [JsonPropertyName("LIVER__DIGESTIVE")]
    public Dictionary<string, string> LiverDigestive { get; set; } = new();

    [JsonPropertyName("KIDNEY__URINARY")]
    public Dictionary<string, string> KidneyUrinary { get; set; } = new();

    [JsonPropertyName("ENDOCRINE__METABOLIC")]
    public Dictionary<string, string> EndocrineMetabolic { get; set; } = new();

    [JsonPropertyName("MUSCULOSKELETAL__AUTOIMMUNE")]
    public Dictionary<string, string> MusculoskeletalAutoimmune { get; set; } = new();

    [JsonPropertyName("LEGAL__BEHAVIORAL")]
    public Dictionary<string, string> LegalBehavioral { get; set; } = new();

    [JsonPropertyName("OTHER")]
    public Dictionary<string, string> Other { get; set; } = new();

    // ================================================================
    // === Catch-all for unmapped or future fields ===
    // ================================================================
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalFields { get; set; }

    // ================================================================
    // === Derived / Computed Helpers ===
    // ================================================================
    [JsonIgnore]
    public string ProductCategory =>
        CoverageType?.Trim().ToUpperInvariant() switch {
            "TERM" or "TERM LIFE" or "TEMPORARY" => "TermLife",
            "WHOLE" or "WHOLE LIFE" or "PERMANENT" => "WholeLife",
            "UNIVERSAL" or "UL" or "IUL" => "UniversalLife",
            _ => "Other"
        };

    // ================================================================
    // === IDomainRuleSet IMPLEMENTATION ===
    // ================================================================
    [JsonIgnore]
    public string? Description =>
        $"Underwriting rule set for {Carrier} ({CoverageType ?? "N/A"}) covering {BenefitRange ?? "any benefit"}";

    public string ToJson() {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public Dictionary<string, object>? GetMetadata() {
        return new Dictionary<string, object>
        {
            { "ruleSetId", RuleSetId },
            { "carrier", Carrier },
            { "coverageType", CoverageType ?? "unknown" },
            { "category", ProductCategory },
            { "ageRange", AgeRange ?? "any" },
            { "benefitRange", BenefitRange ?? "any" }
        };
    }

    public override string ToString()
        => $"{Carrier} ({CoverageType ?? "N/A"}) [{ProductCategory}]";
}
