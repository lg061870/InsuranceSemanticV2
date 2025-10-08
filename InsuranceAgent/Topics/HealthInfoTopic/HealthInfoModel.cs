using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace InsuranceAgent.Topics.HealthInfoTopic
{
    /// <summary>
    /// Model for health information form data.
    /// Inherits from BaseCardModel for continuation support.
    /// </summary>
    public class HealthInfoModel : BaseCardModel
    {
        // Tobacco Use Selections
        [JsonPropertyName("tobacco_yes")]
        public string TobaccoYes { get; set; } = "false";

        [JsonPropertyName("tobacco_no")]
        public string TobaccoNo { get; set; } = "false";

        // Medical Condition Selections
        [JsonPropertyName("condition_diabetes")]
        public string ConditionDiabetes { get; set; } = "false";

        [JsonPropertyName("condition_heart")]
        public string ConditionHeart { get; set; } = "false";

        [JsonPropertyName("condition_bp")]
        public string ConditionBloodPressure { get; set; } = "false";

        [JsonPropertyName("condition_none")]
        public string ConditionNone { get; set; } = "false";

        // Health Insurance Selections
        [JsonPropertyName("insured_yes")]
        public string InsuredYes { get; set; } = "false";

        [JsonPropertyName("insured_no")]
        public string InsuredNo { get; set; } = "false";

        // Physical Measurements
        [JsonPropertyName("height")]
        [StringLength(20, ErrorMessage = "Height cannot exceed 20 characters")]
        public string? Height { get; set; }

        [JsonPropertyName("weight")]
        [StringLength(20, ErrorMessage = "Weight cannot exceed 20 characters")]
        public string? Weight { get; set; }

        // Computed properties for business logic
        public bool? UsesTobacco
        {
            get
            {
                if (TobaccoYes?.ToLower() == "true") return true;
                if (TobaccoNo?.ToLower() == "true") return false;
                return null;
            }
        }

        public List<string> SelectedMedicalConditions
        {
            get
            {
                var conditions = new List<string>();
                if (ConditionDiabetes?.ToLower() == "true") conditions.Add("Diabetes");
                if (ConditionHeart?.ToLower() == "true") conditions.Add("Heart Condition");
                if (ConditionBloodPressure?.ToLower() == "true") conditions.Add("High Blood Pressure");
                if (ConditionNone?.ToLower() == "true") conditions.Add("None Apply");
                return conditions;
            }
        }

        public bool? HasHealthInsurance
        {
            get
            {
                if (InsuredYes?.ToLower() == "true") return true;
                if (InsuredNo?.ToLower() == "true") return false;
                return null;
            }
        }

        // Validation properties
        public bool HasAnsweredTobaccoQuestion => UsesTobacco.HasValue;
        public bool HasSelectedMedicalConditions => SelectedMedicalConditions.Any();
        public bool HasAnsweredInsuranceQuestion => HasHealthInsurance.HasValue;
        public bool HasProvidedHeight => !string.IsNullOrWhiteSpace(Height);
        public bool HasProvidedWeight => !string.IsNullOrWhiteSpace(Weight);

        // Health condition analysis
        public bool HasMedicalConditions => 
            SelectedMedicalConditions.Any() && 
            !SelectedMedicalConditions.Contains("None Apply");

        public bool IsMedicallyHealthy => 
            SelectedMedicalConditions.Contains("None Apply") || 
            !SelectedMedicalConditions.Any();

        public int NumberOfMedicalConditions => 
            SelectedMedicalConditions.Count(c => c != "None Apply");

        // Risk assessment for underwriting
        public string HealthRiskCategory
        {
            get
            {
                var riskScore = 0;
                
                if (UsesTobacco == true) riskScore += 3;
                if (SelectedMedicalConditions.Contains("Diabetes")) riskScore += 2;
                if (SelectedMedicalConditions.Contains("Heart Condition")) riskScore += 3;
                if (SelectedMedicalConditions.Contains("High Blood Pressure")) riskScore += 1;
                
                return riskScore switch
                {
                    0 => "Preferred",
                    1 => "Standard Plus",
                    2 => "Standard",
                    3 => "Substandard",
                    >= 4 => "High Risk",
                    _ => "Unknown"
                };
            }
        }

        // BMI calculation
        public decimal? CalculatedBMI
        {
            get
            {
                var heightInches = ParseHeightToInches();
                var weightPounds = ParseWeightToPounds();
                
                if (heightInches.HasValue && weightPounds.HasValue && heightInches > 0)
                {
                    // BMI = (weight in pounds / (height in inches)²) × 703
                    return Math.Round((weightPounds.Value / (heightInches.Value * heightInches.Value)) * 703, 1);
                }
                return null;
            }
        }

        public string BMICategory
        {
            get
            {
                if (!CalculatedBMI.HasValue) return "Unknown";
                
                return CalculatedBMI.Value switch
                {
                    < 18.5m => "Underweight",
                    >= 18.5m and < 25m => "Normal",
                    >= 25m and < 30m => "Overweight",
                    >= 30m => "Obese"
                };
            }
        }

        // Height parsing helper
        private decimal? ParseHeightToInches()
        {
            if (string.IsNullOrWhiteSpace(Height)) return null;
            
            try
            {
                // Match patterns like 5'10", 5'10, 5 10, 70 inches, etc.
                var feetInchesPattern = @"(\d+)'?\s*(\d+)""?";
                var inchesOnlyPattern = @"^(\d+)\s*(?:inches?|in)?";
                
                var feetInchesMatch = Regex.Match(Height, feetInchesPattern);
                if (feetInchesMatch.Success)
                {
                    var feet = decimal.Parse(feetInchesMatch.Groups[1].Value);
                    var inches = decimal.Parse(feetInchesMatch.Groups[2].Value);
                    return feet * 12 + inches;
                }
                
                var inchesMatch = Regex.Match(Height, inchesOnlyPattern);
                if (inchesMatch.Success)
                {
                    return decimal.Parse(inchesMatch.Groups[1].Value);
                }
            }
            catch
            {
                // Parsing failed
            }
            
            return null;
        }

        // Weight parsing helper
        private decimal? ParseWeightToPounds()
        {
            if (string.IsNullOrWhiteSpace(Weight)) return null;
            
            try
            {
                // Match patterns like 160 lbs, 160, 72.5 kg, etc.
                var poundsPattern = @"([\d.]+)\s*(?:lbs?|pounds?)?";
                var kgPattern = @"([\d.]+)\s*kg";
                
                var kgMatch = Regex.Match(Weight, kgPattern, RegexOptions.IgnoreCase);
                if (kgMatch.Success)
                {
                    var kg = decimal.Parse(kgMatch.Groups[1].Value);
                    return kg * 2.20462m; // Convert kg to pounds
                }
                
                var poundsMatch = Regex.Match(Weight, poundsPattern, RegexOptions.IgnoreCase);
                if (poundsMatch.Success)
                {
                    return decimal.Parse(poundsMatch.Groups[1].Value);
                }
            }
            catch
            {
                // Parsing failed
            }
            
            return null;
        }

        // Data completeness scoring
        public int HealthDataQualityScore
        {
            get
            {
                var score = 0;
                if (HasAnsweredTobaccoQuestion) score += 20;
                if (HasSelectedMedicalConditions) score += 20;
                if (HasAnsweredInsuranceQuestion) score += 20;
                if (HasProvidedHeight) score += 20;
                if (HasProvidedWeight) score += 20;
                return score;
            }
        }

        public string HealthDataQualityGrade => HealthDataQualityScore switch
        {
            >= 90 => "A+",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            _ => "D"
        };

        // Underwriting flags
        public List<string> UnderwritingFlags
        {
            get
            {
                var flags = new List<string>();
                
                if (UsesTobacco == true) flags.Add("Tobacco User - Premium Surcharge");
                if (HasMedicalConditions) flags.Add($"Medical Conditions: {string.Join(", ", SelectedMedicalConditions.Where(c => c != "None Apply"))}");
                if (HasHealthInsurance == false) flags.Add("No Health Insurance - Increased Risk");
                if (BMICategory == "Underweight") flags.Add("Underweight BMI - Medical Review Required");
                if (BMICategory == "Obese") flags.Add("Obese BMI - Premium Surcharge Likely");
                if (CalculatedBMI.HasValue && CalculatedBMI.Value > 35) flags.Add("Severe Obesity - Table Rating Expected");
                
                return flags;
            }
        }

        // Premium impact assessment
        public decimal EstimatedPremiumMultiplier
        {
            get
            {
                decimal multiplier = 1.0m;
                
                if (UsesTobacco == true) multiplier += 0.5m; // 50% surcharge for tobacco
                if (SelectedMedicalConditions.Contains("Diabetes")) multiplier += 0.25m;
                if (SelectedMedicalConditions.Contains("Heart Condition")) multiplier += 0.4m;
                if (SelectedMedicalConditions.Contains("High Blood Pressure")) multiplier += 0.15m;
                
                // BMI adjustments
                if (CalculatedBMI.HasValue)
                {
                    if (CalculatedBMI.Value < 18.5m || CalculatedBMI.Value > 30m) multiplier += 0.1m;
                    if (CalculatedBMI.Value > 35m) multiplier += 0.2m;
                }
                
                return Math.Min(multiplier, 3.0m); // Cap at 300% of standard rates
            }
        }
    }
}