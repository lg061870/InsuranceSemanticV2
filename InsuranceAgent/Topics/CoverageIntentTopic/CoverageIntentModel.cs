using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics.CoverageIntentTopic
{
    /// <summary>
    /// Model for coverage intent form data.
    /// Inherits from BaseCardModel for continuation support.
    /// </summary>
    public class CoverageIntentModel : BaseCardModel
    {
        // Coverage Type Selections
        [JsonPropertyName("coverage_term_life")]
        public string CoverageTermLife { get; set; } = "false";

        [JsonPropertyName("coverage_whole_life")]
        public string CoverageWholeLife { get; set; } = "false";

        [JsonPropertyName("coverage_final_expense")]
        public string CoverageFinalExpense { get; set; } = "false";

        [JsonPropertyName("coverage_health")]
        public string CoverageHealth { get; set; } = "false";

        [JsonPropertyName("coverage_medicare")]
        public string CoverageMedicare { get; set; } = "false";

        [JsonPropertyName("coverage_other")]
        public string CoverageOther { get; set; } = "false";

        // Coverage Start Time Selections
        [JsonPropertyName("coverage_start_asap")]
        public string CoverageStartAsap { get; set; } = "false";

        [JsonPropertyName("coverage_start_30days")]
        public string CoverageStart30Days { get; set; } = "false";

        [JsonPropertyName("coverage_start_1_3mo")]
        public string CoverageStart1To3Months { get; set; } = "false";

        [JsonPropertyName("coverage_start_3mo_plus")]
        public string CoverageStart3MonthsPlus { get; set; } = "false";

        // Coverage Amount Selections
        [JsonPropertyName("coverage_amt_under_50")]
        public string CoverageAmountUnder50k { get; set; } = "false";

        [JsonPropertyName("coverage_amt_50_100")]
        public string CoverageAmount50To100k { get; set; } = "false";

        [JsonPropertyName("coverage_amt_100_250")]
        public string CoverageAmount100To250k { get; set; } = "false";

        [JsonPropertyName("coverage_amt_over_250")]
        public string CoverageAmountOver250k { get; set; } = "false";

        [JsonPropertyName("coverage_amt_unsure")]
        public string CoverageAmountUnsure { get; set; } = "false";

        // Computed properties for business logic
        public List<string> SelectedCoverageTypes
        {
            get
            {
                var types = new List<string>();
                if (CoverageTermLife?.ToLower() == "true") types.Add("Term Life");
                if (CoverageWholeLife?.ToLower() == "true") types.Add("Whole Life");
                if (CoverageFinalExpense?.ToLower() == "true") types.Add("Final Expense");
                if (CoverageHealth?.ToLower() == "true") types.Add("Health Insurance");
                if (CoverageMedicare?.ToLower() == "true") types.Add("Medicare");
                if (CoverageOther?.ToLower() == "true") types.Add("Other");
                return types;
            }
        }

        public string PreferredCoverageStartTime
        {
            get
            {
                if (CoverageStartAsap?.ToLower() == "true") return "ASAP";
                if (CoverageStart30Days?.ToLower() == "true") return "Within 30 Days";
                if (CoverageStart1To3Months?.ToLower() == "true") return "1–3 Months";
                if (CoverageStart3MonthsPlus?.ToLower() == "true") return "3+ Months";
                return "Not specified";
            }
        }

        public string DesiredCoverageAmountBand
        {
            get
            {
                if (CoverageAmountUnder50k?.ToLower() == "true") return "Under $50k";
                if (CoverageAmount50To100k?.ToLower() == "true") return "$50k–$100k";
                if (CoverageAmount100To250k?.ToLower() == "true") return "$100k–$250k";
                if (CoverageAmountOver250k?.ToLower() == "true") return "Over $250k";
                if (CoverageAmountUnsure?.ToLower() == "true") return "Not Sure";
                return "Not specified";
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
}