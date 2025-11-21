using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics;

/// <summary>
/// Model for financial dependents form data.
/// </summary>
public class DependentsModel : BaseCardModel {
    // -----------------------------
    // MARITAL STATUS
    // -----------------------------
    [JsonPropertyName("marital_status")]
    public string? MaritalStatusValue { get; set; }

    public string MaritalStatus =>
        MaritalStatusValue ?? "Not specified";

    // -----------------------------
    // HAS DEPENDENTS (yes/no)
    // -----------------------------
    [JsonPropertyName("has_dependents")]
    public string? HasDependentsValue { get; set; }

    public bool? HasDependents =>
        HasDependentsValue?.ToLower() switch {
            "yes" => true,
            "no" => false,
            _ => null
        };

    // -----------------------------
    // NUMBER OF CHILDREN (explicit)
    // -----------------------------
    [JsonPropertyName("no_of_children")]
    public string? NoOfChildren { get; set; }

    public int ChildrenCount {
        get {
            if (int.TryParse(NoOfChildren, out var n) && n >= 0)
                return n;

            return 0;
        }
    }

    // -----------------------------
    // CHILD AGE RANGES
    // -----------------------------
    [JsonPropertyName("ageRange_0_5")] public string AgeRange0To5 { get; set; } = "false";
    [JsonPropertyName("ageRange_6_12")] public string AgeRange6To12 { get; set; } = "false";
    [JsonPropertyName("ageRange_13_17")] public string AgeRange13To17 { get; set; } = "false";
    [JsonPropertyName("ageRange_18_25")] public string AgeRange18To25 { get; set; } = "false";
    [JsonPropertyName("ageRange_25plus")] public string AgeRange25Plus { get; set; } = "false";

    public List<string> SelectedAgeRanges {
        get {
            var list = new List<string>();

            if (AgeRange0To5 == "true") list.Add("0-5");
            if (AgeRange6To12 == "true") list.Add("6-12");
            if (AgeRange13To17 == "true") list.Add("13-17");
            if (AgeRange18To25 == "true") list.Add("18-25");
            if (AgeRange25Plus == "true") list.Add("Over 25");

            return list;
        }
    }

    // -----------------------------
    // VALIDATION
    // -----------------------------
    public bool HasSelectedMaritalStatus =>
        MaritalStatusValue != null;

    public bool HasAnsweredDependentsQuestion =>
        HasDependents.HasValue;

    public bool HasSelectedAgeRanges =>
        SelectedAgeRanges.Any();

    // -----------------------------
    // FAMILY STRUCTURE ANALYSIS
    // -----------------------------
    public bool IsMarriedOrPartnered =>
        MaritalStatus is "Married" or "Partnered";

    public bool IsSingleParent =>
        (MaritalStatus is "Single" or "Divorced" or "Widowed") &&
        HasDependents == true;

    public bool HasPartner => IsMarriedOrPartnered;

    public bool HasChildren =>
        HasDependents == true;

    public bool IsChildlessCouple =>
        IsMarriedOrPartnered && HasDependents == false;

    // -----------------------------
    // CHILD AGE ANALYSIS
    // -----------------------------
    public bool HasYoungChildren =>
        SelectedAgeRanges.Contains("0-5") || SelectedAgeRanges.Contains("6-12");

    public bool HasTeenageChildren =>
        SelectedAgeRanges.Contains("13-17");

    public bool HasAdultChildren =>
        SelectedAgeRanges.Contains("18-25") ||
        SelectedAgeRanges.Contains("Over 25");

    public bool HasOnlyAdultChildren =>
        HasAdultChildren && !HasYoungChildren && !HasTeenageChildren;

    // -----------------------------
    // FINANCIAL RESPONSIBILITY
    // -----------------------------
    public string FinancialResponsibilityLevel {
        get {
            if (IsSingleParent && HasYoungChildren) return "Very High";
            if (HasYoungChildren && IsMarriedOrPartnered) return "High";
            if (HasTeenageChildren) return "High";
            if (HasOnlyAdultChildren) return "Moderate";
            if (IsChildlessCouple) return "Moderate";
            if (MaritalStatus == "Single" && HasDependents == false) return "Low";
            if (HasDependents == false) return "Low";

            return "Unknown";
        }
    }

    // -----------------------------
    // LIFE INSURANCE NEED LEVEL
    // -----------------------------
    public string LifeInsuranceNeedLevel =>
        IsSingleParent ? "Critical" :
        (HasYoungChildren || HasTeenageChildren) ? "High" :
        (IsMarriedOrPartnered && HasDependents == false) ? "Moderate" :
        HasOnlyAdultChildren ? "Moderate" :
        (MaritalStatus == "Single" && HasDependents == false) ? "Low" :
        "Moderate";

    // -----------------------------
    // COVERAGE MULTIPLIER
    // -----------------------------
    public decimal LifeInsuranceMultiplier {
        get {
            decimal m = 5m;

            if (IsSingleParent) m += 3m;
            else if (HasPartner) m += 1m;

            if (HasYoungChildren) m += 2m;
            else if (HasTeenageChildren) m += 1.5m;
            else if (HasAdultChildren) m += 0.5m;

            return Math.Min(m, 15m);
        }
    }

    // -----------------------------
    // ESTIMATED DEPENDENT COUNT
    // -----------------------------
    public int EstimatedNumberOfDependents {
        get {
            if (HasDependents != true) return 0;

            // Prefer explicit answer if provided
            if (ChildrenCount > 0) return ChildrenCount;

            // Otherwise infer from toggles
            int inferred = SelectedAgeRanges.Count;

            if (inferred == 0) return 1; // minimally assume 1
            if (inferred >= 4) return 3; // cap

            return inferred;
        }
    }

    // -----------------------------
    // COMPLETENESS SCORE
    // -----------------------------
    public int DependentsDataQualityScore {
        get {
            int score = 0;

            if (HasSelectedMaritalStatus) score += 40;
            if (HasAnsweredDependentsQuestion) score += 40;

            if (HasDependents == true && HasSelectedAgeRanges) score += 20;
            else if (HasDependents == false) score += 20;

            return score;
        }
    }

    public string DependentsDataQualityGrade =>
        DependentsDataQualityScore switch {
            >= 90 => "A+",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            _ => "D"
        };

    // -----------------------------
    // UNDERWRITING RISK FACTORS
    // -----------------------------
    public List<string> UnderwritingRiskFactors {
        get {
            var f = new List<string>();

            if (IsSingleParent) f.Add("Single Parent - High Dependency Risk");
            if (HasYoungChildren) f.Add("Young Children - Long-term Financial Obligation");
            if (SelectedAgeRanges.Count >= 3) f.Add("Multiple Age Groups - Extended Coverage Period");
            if (MaritalStatus == "Divorced") f.Add("Divorced - Potential Alimony/Child Support Obligations");
            if (MaritalStatus == "Widowed") f.Add("Widowed - Previous Loss Experience");

            return f;
        }
    }
}
