using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Reflection;

namespace InsuranceAgent.Topics.LifeGoalsTopic
{
    /// <summary>
    /// Custom validation attribute to ensure at least one option is selected
    /// </summary>
    public class RequiredAtLeastOneOptionAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value == null) return false;
            
            // First try to find a HasSelectedOptions property
            var hasSelectedOptionsProperty = value.GetType().GetProperty("HasSelectedOptions");
            if (hasSelectedOptionsProperty != null && hasSelectedOptionsProperty.PropertyType == typeof(bool))
            {
                return (bool)(hasSelectedOptionsProperty.GetValue(value) ?? false);
            }
            
            // Fallback: use reflection to find any boolean properties that are true
            // This checks for properties ending with common boolean option patterns
            var boolProperties = value.GetType().GetProperties()
                .Where(p => p.PropertyType == typeof(bool) && p.CanRead)
                .Where(p => !p.Name.StartsWith("Has") && !p.Name.StartsWith("Is") && !p.Name.StartsWith("Can"))
                .ToList();
            
            return boolProperties.Any(prop => (bool)(prop.GetValue(value) ?? false));
        }

        public override string FormatErrorMessage(string name)
        {
            return "Please select at least one option to continue.";
        }
    }

    /// <summary>
    /// Model for life insurance goals and intentions.
    /// Inherits from BaseCardModel for continuation support.
    /// </summary>
    [RequiredAtLeastOneOption(ErrorMessage = "Please select at least one life insurance goal to continue.")]
    public class LifeGoalsModel : BaseCardModel
    {
        [JsonPropertyName("intent_protect_loved_ones")]
        public string ProtectLovedOnesString { get; set; } = "false";

        [JsonPropertyName("intent_pay_mortgage")]
        public string PayMortgageString { get; set; } = "false";

        [JsonPropertyName("intent_prepare_future")]
        public string PrepareFutureString { get; set; } = "false";

        [JsonPropertyName("intent_peace_of_mind")]
        public string PeaceOfMindString { get; set; } = "false";

        [JsonPropertyName("intent_cover_expenses")]
        public string CoverExpensesString { get; set; } = "false";

        [JsonPropertyName("intent_unsure")]
        public string UnsureString { get; set; } = "false";

        // Computed boolean properties
        public bool ProtectLovedOnes => ProtectLovedOnesString?.ToLower() == "true";
        public bool PayMortgage => PayMortgageString?.ToLower() == "true";
        public bool PrepareFuture => PrepareFutureString?.ToLower() == "true";
        public bool PeaceOfMind => PeaceOfMindString?.ToLower() == "true";
        public bool CoverExpenses => CoverExpensesString?.ToLower() == "true";
        public bool Unsure => UnsureString?.ToLower() == "true";

        // Selected goals analysis
        public List<string> SelectedGoals
        {
            get
            {
                var goals = new List<string>();
                if (ProtectLovedOnes) goals.Add("Protect Loved Ones");
                if (PayMortgage) goals.Add("Pay Off Mortgage");
                if (PrepareFuture) goals.Add("Prepare for Family's Future");
                if (PeaceOfMind) goals.Add("Peace of Mind");
                if (CoverExpenses) goals.Add("Cover Final Expenses");
                if (Unsure) goals.Add("Unsure");
                return goals;
            }
        }

        public int TotalGoalsSelected => SelectedGoals.Count;
        public bool HasSelectedGoals => TotalGoalsSelected > 0;
        public bool HasSelectedOptions => HasSelectedGoals;
        public bool HasMultipleGoals => TotalGoalsSelected > 1;
        public bool IsUnsureOnly => Unsure && TotalGoalsSelected == 1;
        public bool HasSpecificGoals => HasSelectedGoals && !IsUnsureOnly;

        // Goal category analysis
        public bool HasProtectionGoals => ProtectLovedOnes || PrepareFuture || PeaceOfMind;
        public bool HasFinancialGoals => PayMortgage || CoverExpenses;
        public bool HasEmotionalGoals => PeaceOfMind || ProtectLovedOnes;
        public bool HasPracticalGoals => PayMortgage || CoverExpenses;
        public bool HasFamilyFocusedGoals => ProtectLovedOnes || PrepareFuture;

        // Intent confidence scoring
        public int IntentClarityScore
        {
            get
            {
                if (IsUnsureOnly) return 10; // Very low clarity
                if (!HasSelectedGoals) return 0; // No selection
                if (TotalGoalsSelected == 1 && !Unsure) return 100; // Crystal clear
                if (TotalGoalsSelected == 2 && !Unsure) return 85; // Very clear
                if (TotalGoalsSelected == 3 && !Unsure) return 70; // Moderately clear
                if (Unsure && HasSpecificGoals) return 50; // Mixed signals
                return Math.Max(20, 80 - (TotalGoalsSelected * 10)); // Too many goals
            }
        }

        public string IntentClarityLevel => IntentClarityScore switch
        {
            >= 90 => "Crystal Clear",
            >= 70 => "Very Clear",
            >= 50 => "Moderately Clear",
            >= 30 => "Somewhat Unclear",
            _ => "Very Unclear"
        };

        // Primary goal identification
        public string PrimaryGoalCategory
        {
            get
            {
                if (IsUnsureOnly) return "Exploration";
                if (!HasSelectedGoals) return "Undefined";
                
                // Determine primary category based on selected goals
                var protectionCount = (ProtectLovedOnes ? 1 : 0) + (PrepareFuture ? 1 : 0) + (PeaceOfMind ? 1 : 0);
                var financialCount = (PayMortgage ? 1 : 0) + (CoverExpenses ? 1 : 0);
                
                if (protectionCount > financialCount) return "Family Protection";
                if (financialCount > protectionCount) return "Financial Security";
                if (protectionCount == financialCount && protectionCount > 0) return "Comprehensive Planning";
                
                return "Mixed Objectives";
            }
        }

        // Product recommendation scoring
        public int TermLifeAffinityScore
        {
            get
            {
                var score = 0;
                if (PayMortgage) score += 40; // Perfect for mortgage protection
                if (ProtectLovedOnes) score += 30; // Core term life purpose
                if (PrepareFuture) score += 20; // Temporary high coverage need
                if (CoverExpenses) score += 15; // Can work for final expenses
                if (PeaceOfMind) score += 10; // Basic peace of mind
                if (Unsure) score -= 10; // Uncertainty reduces fit
                return Math.Max(0, score);
            }
        }

        public int WholeLifeAffinityScore
        {
            get
            {
                var score = 0;
                if (PrepareFuture) score += 40; // Perfect for long-term planning
                if (PeaceOfMind) score += 30; // Permanent coverage comfort
                if (CoverExpenses) score += 25; // Guaranteed final expense coverage
                if (ProtectLovedOnes) score += 20; // Permanent protection
                if (PayMortgage) score += 10; // Can work but not optimal
                if (Unsure) score -= 15; // Complex products need clarity
                return Math.Max(0, score);
            }
        }

        public int UniversalLifeAffinityScore
        {
            get
            {
                var score = 0;
                if (PrepareFuture) score += 35; // Flexible long-term planning
                if (PayMortgage) score += 25; // Flexible premiums for mortgage
                if (ProtectLovedOnes) score += 20; // Adjustable protection
                if (PeaceOfMind) score += 15; // Permanent with flexibility
                if (CoverExpenses) score += 15; // Can adjust for final expenses
                if (Unsure) score -= 20; // Most complex, needs clarity
                return Math.Max(0, score);
            }
        }

        // Recommended product type
        public string RecommendedProductType
        {
            get
            {
                var termScore = TermLifeAffinityScore;
                var wholeScore = WholeLifeAffinityScore;
                var universalScore = UniversalLifeAffinityScore;
                
                var maxScore = Math.Max(termScore, Math.Max(wholeScore, universalScore));
                
                if (maxScore == termScore) return "Term Life";
                if (maxScore == wholeScore) return "Whole Life";
                if (maxScore == universalScore) return "Universal Life";
                
                return "Needs Assessment Required";
            }
        }

        // Coverage amount estimation factors
        public decimal CoverageMultiplierFactor
        {
            get
            {
                decimal factor = 1.0m;
                
                if (PayMortgage) factor += 0.5m; // Mortgage typically significant
                if (ProtectLovedOnes) factor += 0.3m; // Family protection needs
                if (PrepareFuture) factor += 0.4m; // Long-term planning
                if (CoverExpenses) factor += 0.1m; // Final expenses
                if (PeaceOfMind) factor += 0.2m; // Comfort level increase
                
                if (HasMultipleGoals) factor += 0.2m; // Multiple needs
                if (Unsure) factor *= 0.8m; // Reduce for uncertainty
                
                return Math.Max(1.0m, factor);
            }
        }

        // Urgency assessment
        public string UrgencyLevel
        {
            get
            {
                if (IsUnsureOnly) return "Low - Education Needed";
                if (!HasSelectedGoals) return "Unknown";
                
                if (PayMortgage && ProtectLovedOnes) return "High - Immediate Protection";
                if (ProtectLovedOnes && HasFamilyFocusedGoals) return "High - Family Dependent";
                if (PayMortgage || CoverExpenses) return "Medium - Financial Obligation";
                if (PeaceOfMind && PrepareFuture) return "Medium - Planning Focused";
                
                return "Standard - General Interest";
            }
        }

        // Sales approach recommendations
        public List<string> SalesApproachRecommendations
        {
            get
            {
                var recommendations = new List<string>();
                
                if (IsUnsureOnly)
                {
                    recommendations.Add("🎓 Education-First Approach: Focus on life insurance basics");
                    recommendations.Add("📈 Needs Analysis: Conduct thorough financial review");
                    recommendations.Add("💭 Consultative Selling: Ask discovery questions");
                }
                else if (HasSpecificGoals)
                {
                    if (PayMortgage)
                        recommendations.Add("🏠 Mortgage Protection Focus: Calculate exact mortgage balance and term");
                    
                    if (ProtectLovedOnes)
                        recommendations.Add("👨‍👩‍👧‍👦 Family Protection: Discuss income replacement needs");
                    
                    if (PrepareFuture)
                        recommendations.Add("🔮 Future Planning: Present permanent life options with cash value");
                    
                    if (CoverExpenses)
                        recommendations.Add("⚰️ Final Expense: Focus on guaranteed coverage and simplified underwriting");
                    
                    if (PeaceOfMind)
                        recommendations.Add("🧘 Peace of Mind: Emphasize guaranteed benefits and stability");
                }
                
                // Product-specific recommendations
                recommendations.Add($"🎯 Primary Product Focus: {RecommendedProductType}");
                
                if (IntentClarityScore >= 70)
                    recommendations.Add("⚡ Direct Approach: Client has clear goals, move to product presentation");
                else
                    recommendations.Add("🔍 Discovery Needed: Clarify goals before product recommendation");
                
                return recommendations;
            }
        }

        // Goal-based insights
        public List<string> GoalInsights
        {
            get
            {
                var insights = new List<string>();
                
                insights.Add($"Goals Selected: {TotalGoalsSelected}/6");
                insights.Add($"Intent Clarity: {IntentClarityLevel} ({IntentClarityScore}/100)");
                insights.Add($"Primary Category: {PrimaryGoalCategory}");
                insights.Add($"Recommended Product: {RecommendedProductType}");
                insights.Add($"Urgency Level: {UrgencyLevel}");
                insights.Add($"Coverage Factor: {CoverageMultiplierFactor:F1}x base need");
                
                if (HasProtectionGoals && HasFinancialGoals)
                    insights.Add("🎯 Balanced Needs: Both protection and financial goals identified");
                
                if (HasMultipleGoals && !Unsure)
                    insights.Add("📈 Comprehensive Planning: Multiple specific goals suggest higher coverage need");
                
                if (Unsure && HasSpecificGoals)
                    insights.Add("⚠️ Mixed Signals: Has specific goals but also unsure - needs clarification");
                
                return insights;
            }
        }

        // Data quality assessment
        public int GoalDataQualityScore
        {
            get
            {
                if (!HasSelectedGoals) return 0; // No data
                if (IsUnsureOnly) return 25; // Minimal useful data
                
                var score = 40; // Base for having some selection
                score += TotalGoalsSelected * 10; // Points per goal
                
                if (HasSpecificGoals) score += 20; // Bonus for specific goals
                if (IntentClarityScore >= 70) score += 15; // Clarity bonus
                if (HasProtectionGoals && HasFinancialGoals) score += 10; // Comprehensive
                
                return Math.Min(100, score);
            }
        }

        public string GoalDataQualityGrade => GoalDataQualityScore switch
        {
            >= 90 => "A+",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            >= 40 => "D",
            _ => "F"
        };
    }
}