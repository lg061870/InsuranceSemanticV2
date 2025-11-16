using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics;

/// <summary>
/// Model for coverage intent form data.
/// Inherits from BaseCardModel for continuation support.
/// </summary>
public class CoverageIntentModel : BaseCardModel
{
    // Single selection fields instead of multiple toggles
    [JsonPropertyName("coverage_type")]
    public string? CoverageType { get; set; }

    [JsonPropertyName("coverage_start_time")]
    public string? CoverageStartTime { get; set; }

    [JsonPropertyName("coverage_amount")]
    public string? CoverageAmount { get; set; }

    // Computed properties for business logic
    public List<string> SelectedCoverageTypes
    {
        get
        {
            var types = new List<string>();
            if (!string.IsNullOrEmpty(CoverageType))
            {
                var displayName = CoverageType switch
                {
                    "term_life" => "Term Life",
                    "whole_life" => "Whole Life", 
                    "final_expense" => "Final Expense",
                    "health_insurance" => "Health Insurance",
                    "medicare" => "Medicare",
                    "other" => "Other",
                    _ => CoverageType
                };
                types.Add(displayName);
            }
            return types;
        }
    }

    public string PreferredCoverageStartTime
    {
        get
        {
            return CoverageStartTime switch
            {
                "asap" => "ASAP",
                "30_days" => "Within 30 Days",
                "1_3_months" => "1–3 Months",
                "3_months_plus" => "3+ Months",
                _ => "Not specified"
            };
        }
    }

    public string DesiredCoverageAmountBand
    {
        get
        {
            return CoverageAmount switch
            {
                "under_50k" => "Under $50k",
                "50k_100k" => "$50k–$100k",
                "100k_250k" => "$100k–$250k",
                "over_250k" => "Over $250k", 
                "not_sure" => "Not Sure",
                _ => "Not specified"
            };
        }
    }

    // Validation properties
    public bool HasSelectedCoverageType => SelectedCoverageTypes.Any();
    public bool HasSelectedStartTime => PreferredCoverageStartTime != "Not specified";
    public bool HasSelectedCoverageAmount => DesiredCoverageAmountBand != "Not specified";

    // Business logic properties
    public bool IsLifeInsuranceInterest => 
        SelectedCoverageTypes.Contains("Term Life") || 
        SelectedCoverageTypes.Contains("Whole Life") || 
        SelectedCoverageTypes.Contains("Final Expense");

    public bool IsHealthInsuranceInterest => 
        SelectedCoverageTypes.Contains("Health Insurance") || 
        SelectedCoverageTypes.Contains("Medicare");

    public bool IsUrgentTimeframe => 
        PreferredCoverageStartTime == "ASAP" || 
        PreferredCoverageStartTime == "Within 30 Days";

    public bool IsHighValueCoverage => 
        DesiredCoverageAmountBand == "$100k–$250k" || 
        DesiredCoverageAmountBand == "Over $250k";

    // Coverage amount estimation (mid-point of range)
    public decimal? EstimatedCoverageAmount
    {
        get
        {
            return DesiredCoverageAmountBand switch
            {
                "Under $50k" => 25000m,
                "$50k–$100k" => 75000m,
                "$100k–$250k" => 175000m,
                "Over $250k" => 500000m, // Conservative estimate
                "Not Sure" => null,
                _ => null
            };
        }
    }

    // Lead scoring based on intent clarity
    public int IntentClarityScore
    {
        get
        {
            var score = 0;
            if (HasSelectedCoverageType) score += 40;
            if (HasSelectedStartTime) score += 30;
            if (HasSelectedCoverageAmount && DesiredCoverageAmountBand != "Not Sure") score += 30;
            return score;
        }
    }

    public string IntentClarityGrade => IntentClarityScore switch
    {
        >= 90 => "A+",
        >= 80 => "A",
        >= 70 => "B",
        >= 60 => "C",
        _ => "D"
    };

    // Priority scoring for sales follow-up
    public int SalesPriorityScore
    {
        get
        {
            var score = IntentClarityScore;
            if (IsUrgentTimeframe) score += 20;
            if (IsHighValueCoverage) score += 15;
            if (IsLifeInsuranceInterest) score += 10; // Life insurance typically higher margin
            return Math.Min(100, score);
        }
    }

    public string SalesPriorityLevel => SalesPriorityScore switch
    {
        >= 90 => "Critical",
        >= 75 => "High",
        >= 60 => "Medium",
        >= 40 => "Low",
        _ => "Follow-up"
    };
}