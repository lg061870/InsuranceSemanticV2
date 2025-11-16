using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace InsuranceAgent.Topics;

/// <summary>
/// Model for insurance context form data.
/// Inherits from BaseCardModel for continuation support.
/// </summary>
public class InsuranceContextModel : BaseCardModel
{
    [JsonPropertyName("insurance_type")]
    [StringLength(100, ErrorMessage = "Insurance type cannot exceed 100 characters")]
    public string? InsuranceType { get; set; }

    [JsonPropertyName("coverage_for")]
    [StringLength(100, ErrorMessage = "Coverage for cannot exceed 100 characters")]
    public string? CoverageFor { get; set; }

    [JsonPropertyName("coverage_goal")]
    [StringLength(200, ErrorMessage = "Coverage goal cannot exceed 200 characters")]
    public string? CoverageGoal { get; set; }

    [JsonPropertyName("insurance_target")]
    [StringLength(50, ErrorMessage = "Insurance target cannot exceed 50 characters")]
    public string? InsuranceTarget { get; set; }

    [JsonPropertyName("home_value")]
    public string? HomeValueString { get; set; }

    [JsonPropertyName("mortgage_balance")]
    public string? MortgageBalanceString { get; set; }

    [JsonPropertyName("monthly_mortgage")]
    public string? MonthlyMortgageString { get; set; }

    [JsonPropertyName("loan_term")]
    public string? LoanTermString { get; set; }

    [JsonPropertyName("equity")]
    public string? EquityString { get; set; }

    [JsonPropertyName("has_existing_life_insurance")]
    public string HasExistingLifeInsuranceString { get; set; } = "false";

    [JsonPropertyName("existing_life_insurance_coverage")]
    [StringLength(50, ErrorMessage = "Existing coverage cannot exceed 50 characters")]
    public string? ExistingLifeInsuranceCoverage { get; set; }

    // Computed properties for business logic
    public decimal? HomeValue => ParseDecimal(HomeValueString);
    public decimal? MortgageBalance => ParseDecimal(MortgageBalanceString);
    public decimal? MonthlyMortgage => ParseDecimal(MonthlyMortgageString);
    public int? LoanTerm => ParseInt(LoanTermString);
    public decimal? Equity => ParseDecimal(EquityString);

    public bool? HasExistingLifeInsurance
    {
        get
        {
            if (HasExistingLifeInsuranceString?.ToLower() == "true") return true;
            if (HasExistingLifeInsuranceString?.ToLower() == "false") return false;
            return null;
        }
    }

    // Validation properties
    public bool HasProvidedInsuranceType => !string.IsNullOrWhiteSpace(InsuranceType);
    public bool HasProvidedCoverageFor => !string.IsNullOrWhiteSpace(CoverageFor);
    public bool HasProvidedCoverageGoal => !string.IsNullOrWhiteSpace(CoverageGoal);
    public bool HasProvidedInsuranceTarget => !string.IsNullOrWhiteSpace(InsuranceTarget);
    public bool HasProvidedHomeValue => HomeValue.HasValue;
    public bool HasProvidedMortgageInfo => MortgageBalance.HasValue || MonthlyMortgage.HasValue;
    public bool HasProvidedExistingCoverage => !string.IsNullOrWhiteSpace(ExistingLifeInsuranceCoverage);

    // Financial analysis
    public decimal? CalculatedEquity
    {
        get
        {
            if (HomeValue.HasValue && MortgageBalance.HasValue)
            {
                return HomeValue.Value - MortgageBalance.Value;
            }
            return Equity; // Use provided equity if calculation not possible
        }
    }

    public decimal? LoanToValueRatio
    {
        get
        {
            if (HomeValue.HasValue && MortgageBalance.HasValue && HomeValue.Value > 0)
            {
                return MortgageBalance.Value / HomeValue.Value * 100;
            }
            return null;
        }
    }

    public decimal? EquityPercentage
    {
        get
        {
            if (HomeValue.HasValue && CalculatedEquity.HasValue && HomeValue.Value > 0)
            {
                return CalculatedEquity.Value / HomeValue.Value * 100;
            }
            return null;
        }
    }

    // Coverage analysis
    public decimal? ParsedInsuranceTarget
    {
        get
        {
            if (string.IsNullOrWhiteSpace(InsuranceTarget)) return null;
            
            // Remove currency symbols, commas, and extract numbers
            var cleaned = Regex.Replace(InsuranceTarget, @"[^\d.,]", "");
            if (decimal.TryParse(cleaned, out var result))
            {
                return result;
            }
            return null;
        }
    }

    public decimal? ParsedExistingCoverage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ExistingLifeInsuranceCoverage)) return null;
            
            var cleaned = Regex.Replace(ExistingLifeInsuranceCoverage, @"[^\d.,]", "");
            if (decimal.TryParse(cleaned, out var result))
            {
                return result;
            }
            return null;
        }
    }

    public decimal? AdditionalCoverageNeeded
    {
        get
        {
            if (ParsedInsuranceTarget.HasValue)
            {
                var existing = ParsedExistingCoverage ?? 0;
                return Math.Max(0, ParsedInsuranceTarget.Value - existing);
            }
            return null;
        }
    }

    // Coverage adequacy assessment
    public string CoverageAdequacyAssessment
    {
        get
        {
            if (!ParsedInsuranceTarget.HasValue) return "Target Not Specified";
            
            var target = ParsedInsuranceTarget.Value;
            var existing = ParsedExistingCoverage ?? 0;
            
            if (existing >= target) return "Adequately Covered";
            if (existing >= target * 0.8m) return "Nearly Adequate";
            if (existing >= target * 0.5m) return "Partially Covered";
            if (existing > 0) return "Significantly Under-Covered";
            return "No Existing Coverage";
        }
    }

    // Mortgage protection analysis
    public bool IsMortgageProtectionGoal => 
        CoverageGoal?.ToLower().Contains("mortgage") == true ||
        CoverageGoal?.ToLower().Contains("debt") == true;

    public bool IsIncomeReplacementGoal => 
        CoverageGoal?.ToLower().Contains("income") == true ||
        CoverageGoal?.ToLower().Contains("salary") == true;

    public decimal? MortgageProtectionNeed
    {
        get
        {
            if (IsMortgageProtectionGoal && MortgageBalance.HasValue)
            {
                return MortgageBalance.Value;
            }
            return null;
        }
    }

    // Data completeness scoring
    public int InsuranceContextDataQualityScore
    {
        get
        {
            var score = 0;
            if (HasProvidedInsuranceType) score += 15;
            if (HasProvidedCoverageFor) score += 10;
            if (HasProvidedCoverageGoal) score += 15;
            if (HasProvidedInsuranceTarget) score += 20;
            if (HasProvidedHomeValue) score += 10;
            if (HasProvidedMortgageInfo) score += 10;
            if (HasExistingLifeInsurance.HasValue) score += 10;
            if (HasExistingLifeInsurance == true && HasProvidedExistingCoverage) score += 10;
            return score;
        }
    }

    public string InsuranceContextDataQualityGrade => InsuranceContextDataQualityScore switch
    {
        >= 90 => "A+",
        >= 80 => "A",
        >= 70 => "B",
        >= 60 => "C",
        _ => "D"
    };

    // Lead qualification factors
    public List<string> QualificationFactors
    {
        get
        {
            var factors = new List<string>();
            
            if (ParsedInsuranceTarget.HasValue && ParsedInsuranceTarget.Value >= 100000)
                factors.Add("High Value Target - Premium Opportunity");
                
            if (HasExistingLifeInsurance == false)
                factors.Add("No Existing Coverage - Primary Need");
                
            if (IsMortgageProtectionGoal && MortgageBalance.HasValue)
                factors.Add($"Mortgage Protection Need: ${MortgageBalance.Value:N0}");
                
            if (IsIncomeReplacementGoal)
                factors.Add("Income Replacement Focus - Long-term Need");
                
            if (HomeValue.HasValue && HomeValue.Value >= 500000)
                factors.Add("High Net Worth - Affluent Market");
                
            if (LoanToValueRatio.HasValue && LoanToValueRatio.Value > 80)
                factors.Add("High LTV - Mortgage Protection Critical");
                
            return factors;
        }
    }

    // Helper methods
    private decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (decimal.TryParse(value, out var result)) return result;
        return null;
    }

    private int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, out var result)) return result;
        return null;
    }

    // Priority scoring for sales
    public int SalesPriorityScore
    {
        get
        {
            var score = InsuranceContextDataQualityScore;
            
            if (ParsedInsuranceTarget.HasValue)
            {
                if (ParsedInsuranceTarget.Value >= 500000) score += 20;
                else if (ParsedInsuranceTarget.Value >= 250000) score += 15;
                else if (ParsedInsuranceTarget.Value >= 100000) score += 10;
            }
            
            if (HasExistingLifeInsurance == false) score += 10;
            if (IsMortgageProtectionGoal) score += 5;
            if (HomeValue.HasValue && HomeValue.Value >= 400000) score += 10;
            
            return Math.Min(score, 100);
        }
    }

    public string SalesPriorityLevel => SalesPriorityScore switch
    {
        >= 90 => "Critical",
        >= 75 => "High",
        >= 60 => "Medium",
        >= 40 => "Standard",
        _ => "Low"
    };
}