using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics;

/// <summary>
/// Model for employment information form data.
/// Inherits from BaseCardModel for continuation support.
/// </summary>
public class EmploymentModel : BaseCardModel
{
    // Employment Status Selection (single choice)
    [JsonPropertyName("employment_status")]
    public string? EmploymentStatusValue { get; set; }

    // Income Band Selection (single choice)
    [JsonPropertyName("household_income")]
    public string? HouseholdIncomeValue { get; set; }

    // Occupation
    [JsonPropertyName("occupation")]
    [StringLength(100, ErrorMessage = "Occupation cannot exceed 100 characters")]
    public string? Occupation { get; set; }

    // Computed properties for business logic
    public string EmploymentStatus
    {
        get
        {
            return EmploymentStatusValue ?? "Not specified";
        }
    }

    public string HouseholdIncomeBand
    {
        get
        {
            return HouseholdIncomeValue ?? "Not specified";
        }
    }

    // Validation properties
    public bool HasSelectedEmploymentStatus => EmploymentStatus != "Not specified";
    public bool HasSelectedIncomeRange => HouseholdIncomeBand != "Not specified";
    public bool HasProvidedOccupation => !string.IsNullOrWhiteSpace(Occupation);

    // Employment categorization
    public bool IsEmployed => 
        EmploymentStatus == "Full-Time" || 
        EmploymentStatus == "Part-Time" || 
        EmploymentStatus == "Self-Employed";

    public bool IsNotCurrentlyEmployed => 
        EmploymentStatus == "Unemployed" || 
        EmploymentStatus == "Retired" || 
        EmploymentStatus == "Student";

    public bool IsFullTimeEquivalent => 
        EmploymentStatus == "Full-Time" || 
        EmploymentStatus == "Self-Employed";

    // Income analysis
    public bool IsLowIncomeRange => 
        HouseholdIncomeBand == "Under $25k" || 
        HouseholdIncomeBand == "$25k–$50k";

    public bool IsMiddleIncomeRange => 
        HouseholdIncomeBand == "$50k–$75k" || 
        HouseholdIncomeBand == "$75k–$100k";

    public bool IsHighIncomeRange => 
        HouseholdIncomeBand == "Over $100k";

    public bool IncomeDisclosed => 
        HouseholdIncomeBand != "Prefer not to say" && 
        HouseholdIncomeBand != "Not specified";

    // Income estimation (mid-point of range)
    public decimal? EstimatedHouseholdIncome
    {
        get
        {
            return HouseholdIncomeBand switch
            {
                "Under $25k" => 15000m,
                "$25k–$50k" => 37500m,
                "$50k–$75k" => 62500m,
                "$75k–$100k" => 87500m,
                "Over $100k" => 125000m, // Conservative estimate
                "Prefer not to say" => null,
                "Not specified" => null,
                _ => null
            };
        }
    }

    // Risk assessment for underwriting
    public string EmploymentRiskCategory
    {
        get
        {
            if (EmploymentStatus == "Full-Time" && IsHighIncomeRange) return "Low Risk";
            if (IsEmployed && IsMiddleIncomeRange) return "Standard Risk";
            if (EmploymentStatus == "Retired" && !IsLowIncomeRange) return "Standard Risk";
            if (EmploymentStatus == "Self-Employed") return "Moderate Risk";
            if (EmploymentStatus == "Part-Time" || IsLowIncomeRange) return "Moderate Risk";
            if (EmploymentStatus == "Unemployed" || EmploymentStatus == "Student") return "High Risk";
            return "Unknown Risk";
        }
    }

    // Lead quality scoring
    public int EmploymentDataQualityScore
    {
        get
        {
            var score = 0;
            if (HasSelectedEmploymentStatus) score += 40;
            if (HasSelectedIncomeRange && IncomeDisclosed) score += 40;
            if (HasProvidedOccupation) score += 20;
            return score;
        }
    }

    public string EmploymentDataQualityGrade => EmploymentDataQualityScore switch
    {
        >= 90 => "A+",
        >= 80 => "A",
        >= 70 => "B",
        >= 60 => "C",
        _ => "D"
    };

    // Affordability assessment
    public bool CanLikelyAffordInsurance
    {
        get
        {
            if (!IncomeDisclosed) return false; // Can't assess without income info
            
            // Basic affordability logic
            if (IsHighIncomeRange) return true;
            if (IsMiddleIncomeRange && IsEmployed) return true;
            if (EmploymentStatus == "Retired" && !IsLowIncomeRange) return true;
            
            return false;
        }
    }
}