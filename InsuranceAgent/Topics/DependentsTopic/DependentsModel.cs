using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics.DependentsTopic
{
    /// <summary>
    /// Model for financial dependents form data.
    /// Inherits from BaseCardModel for continuation support.
    /// </summary>
    public class DependentsModel : BaseCardModel
    {
        // Marital Status Selections
        [JsonPropertyName("marital_single")]
        public string MaritalSingle { get; set; } = "false";

        [JsonPropertyName("marital_married")]
        public string MaritalMarried { get; set; } = "false";

        [JsonPropertyName("marital_partnered")]
        public string MaritalPartnered { get; set; } = "false";

        [JsonPropertyName("marital_divorced")]
        public string MaritalDivorced { get; set; } = "false";

        [JsonPropertyName("marital_widowed")]
        public string MaritalWidowed { get; set; } = "false";

        // Has Dependents Selections
        [JsonPropertyName("hasDependents_yes")]
        public string HasDependentsYes { get; set; } = "false";

        [JsonPropertyName("hasDependents_no")]
        public string HasDependentsNo { get; set; } = "false";

        // Age Range Selections
        [JsonPropertyName("ageRange_0_5")]
        public string AgeRange0To5 { get; set; } = "false";

        [JsonPropertyName("ageRange_6_12")]
        public string AgeRange6To12 { get; set; } = "false";

        [JsonPropertyName("ageRange_13_17")]
        public string AgeRange13To17 { get; set; } = "false";

        [JsonPropertyName("ageRange_18_25")]
        public string AgeRange18To25 { get; set; } = "false";

        [JsonPropertyName("ageRange_25plus")]
        public string AgeRange25Plus { get; set; } = "false";

        // Computed properties for business logic
        public string MaritalStatus
        {
            get
            {
                if (MaritalSingle?.ToLower() == "true") return "Single";
                if (MaritalMarried?.ToLower() == "true") return "Married";
                if (MaritalPartnered?.ToLower() == "true") return "Partnered";
                if (MaritalDivorced?.ToLower() == "true") return "Divorced";
                if (MaritalWidowed?.ToLower() == "true") return "Widowed";
                return "Not specified";
            }
        }

        public bool? HasDependents
        {
            get
            {
                if (HasDependentsYes?.ToLower() == "true") return true;
                if (HasDependentsNo?.ToLower() == "true") return false;
                return null;
            }
        }

        public List<string> SelectedAgeRanges
        {
            get
            {
                var ranges = new List<string>();
                if (AgeRange0To5?.ToLower() == "true") ranges.Add("0-5");
                if (AgeRange6To12?.ToLower() == "true") ranges.Add("6-12");
                if (AgeRange13To17?.ToLower() == "true") ranges.Add("13-17");
                if (AgeRange18To25?.ToLower() == "true") ranges.Add("18-25");
                if (AgeRange25Plus?.ToLower() == "true") ranges.Add("Over 25");
                return ranges;
            }
        }

        // Validation properties
        public bool HasSelectedMaritalStatus => MaritalStatus != "Not specified";
        public bool HasAnsweredDependentsQuestion => HasDependents.HasValue;
        public bool HasSelectedAgeRanges => SelectedAgeRanges.Any();

        // Family structure analysis
        public bool IsMarriedOrPartnered => 
            MaritalStatus == "Married" || 
            MaritalStatus == "Partnered";

        public bool IsSingleParent => 
            (MaritalStatus == "Single" || MaritalStatus == "Divorced" || MaritalStatus == "Widowed") && 
            HasDependents == true;

        public bool HasPartner => IsMarriedOrPartnered;
        
        public bool HasChildren => HasDependents == true && HasSelectedAgeRanges;

        public bool IsChildlessCouple => IsMarriedOrPartnered && HasDependents == false;

        // Children age analysis
        public bool HasYoungChildren => 
            SelectedAgeRanges.Contains("0-5") || 
            SelectedAgeRanges.Contains("6-12");

        public bool HasTeenageChildren => 
            SelectedAgeRanges.Contains("13-17");

        public bool HasAdultChildren => 
            SelectedAgeRanges.Contains("18-25") || 
            SelectedAgeRanges.Contains("Over 25");

        public bool HasOnlyAdultChildren => 
            HasAdultChildren && !HasYoungChildren && !HasTeenageChildren;

        // Financial responsibility assessment
        public string FinancialResponsibilityLevel
        {
            get
            {
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

        // Life insurance need assessment
        public string LifeInsuranceNeedLevel
        {
            get
            {
                if (IsSingleParent) return "Critical";
                if (HasYoungChildren || HasTeenageChildren) return "High";
                if (IsMarriedOrPartnered && HasDependents == false) return "Moderate";
                if (HasOnlyAdultChildren) return "Moderate";
                if (MaritalStatus == "Single" && HasDependents == false) return "Low";
                return "Moderate";
            }
        }

        // Coverage amount suggestion factors
        public decimal LifeInsuranceMultiplier
        {
            get
            {
                decimal baseMultiplier = 5.0m; // Base 5x annual income
                
                if (IsSingleParent) baseMultiplier += 3.0m;
                else if (HasPartner) baseMultiplier += 1.0m;
                
                if (HasYoungChildren) baseMultiplier += 2.0m;
                else if (HasTeenageChildren) baseMultiplier += 1.5m;
                else if (HasAdultChildren) baseMultiplier += 0.5m;
                
                return Math.Min(baseMultiplier, 15.0m); // Cap at 15x income
            }
        }

        // Data completeness scoring
        public int DependentsDataQualityScore
        {
            get
            {
                var score = 0;
                if (HasSelectedMaritalStatus) score += 40;
                if (HasAnsweredDependentsQuestion) score += 40;
                if (HasDependents == true && HasSelectedAgeRanges) score += 20;
                else if (HasDependents == false) score += 20; // Complete if no dependents
                return score;
            }
        }

        public string DependentsDataQualityGrade => DependentsDataQualityScore switch
        {
            >= 90 => "A+",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            _ => "D"
        };

        // Risk factors for underwriting
        public List<string> UnderwritingRiskFactors
        {
            get
            {
                var factors = new List<string>();
                
                if (IsSingleParent) factors.Add("Single Parent - High Dependency Risk");
                if (HasYoungChildren) factors.Add("Young Children - Long-term Financial Obligation");
                if (SelectedAgeRanges.Count >= 3) factors.Add("Multiple Age Groups - Extended Coverage Period");
                if (MaritalStatus == "Divorced") factors.Add("Divorced - Potential Alimony/Child Support Obligations");
                if (MaritalStatus == "Widowed") factors.Add("Widowed - Previous Loss Experience");
                
                return factors;
            }
        }

        // Estimated number of dependents (rough calculation)
        public int EstimatedNumberOfDependents
        {
            get
            {
                if (HasDependents != true) return 0;
                
                // Rough estimate based on age ranges selected
                var estimate = SelectedAgeRanges.Count;
                
                // Adjust for typical family patterns
                if (estimate == 0) return HasDependents == true ? 1 : 0;
                if (estimate >= 4) return 3; // Cap realistic estimate
                
                return estimate;
            }
        }
    }
}