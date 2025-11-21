using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics;

/// <summary>
/// Model for employment information form data.
/// Inherits from BaseCardModel for continuation support.
/// </summary>
public class EmploymentModel : BaseCardModel {
    // Employment Status (TagSelect - single)
    [JsonPropertyName("employment_status")]
    public string? EmploymentStatusValue { get; set; }

    // Household income selection (TagSelect - single)
    [JsonPropertyName("household_income")]
    public string? HouseholdIncomeValue { get; set; }

    // Occupation
    [JsonPropertyName("occupation")]
    [StringLength(100, ErrorMessage = "Occupation cannot exceed 100 characters")]
    public string? Occupation { get; set; }

    [JsonPropertyName("years_employed")]
    public string? YearsEmployedValue { get; set; }

    // Computed business properties
    public string EmploymentStatus => EmploymentStatusValue ?? "N/A";

    public string HouseholdIncomeBand => HouseholdIncomeValue ?? "N/A";

    public bool HasEmploymentStatus => !string.IsNullOrWhiteSpace(EmploymentStatusValue);
    public bool HasIncomeBand => !string.IsNullOrWhiteSpace(HouseholdIncomeValue);
    public bool HasOccupation => !string.IsNullOrWhiteSpace(Occupation);

    // Category rules
    public bool IsEmployed =>
        EmploymentStatus is "Full-Time" or "Part-Time" or "Self-Employed";

    public bool IsNotWorking =>
        EmploymentStatus is "Unemployed" or "Retired" or "Student";

    // Income classification
    public bool IsLowIncome =>
        HouseholdIncomeBand is "Under $25k" or "$25k–$50k";

    public bool IsMiddleIncome =>
        HouseholdIncomeBand is "$50k–$75k" or "$75k–$100k";

    public bool IsHighIncome =>
        HouseholdIncomeBand == "Over $100k";

    // Estimated numerical income (midpoints)
    public decimal? EstimatedIncome => HouseholdIncomeBand switch {
        "Under $25k" => 15000m,
        "$25k–$50k" => 37500m,
        "$50k–$75k" => 62500m,
        "$75k–$100k" => 87500m,
        "Over $100k" => 125000m,
        "Prefer not to say" => null,
        _ => null
    };

    // Underwriting risk
    public string RiskCategory =>
        (EmploymentStatus, IsHighIncome, IsLowIncome) switch {
            ("Full-Time", true, _) => "Low Risk",
            ("Self-Employed", _, _) => "Moderate Risk",
            ("Retired", _, false) => "Standard Risk",
            (_, _, true) => "Moderate Risk",
            ("Unemployed", _, _) => "High Risk",
            ("Student", _, _) => "High Risk",
            _ => "Unknown"
        };

    // Lead quality scoring
    public int DataQualityScore =>
        (HasEmploymentStatus ? 40 : 0) +
        (HasIncomeBand ? 40 : 0) +
        (HasOccupation ? 20 : 0);

    public string DataQualityGrade => DataQualityScore switch {
        >= 90 => "A+",
        >= 80 => "A",
        >= 70 => "B",
        >= 60 => "C",
        _ => "D"
    };

    public bool CanAffordInsurance =>
        IsHighIncome ||
        (IsMiddleIncome && IsEmployed) ||
        (EmploymentStatus == "Retired" && !IsLowIncome);
}
