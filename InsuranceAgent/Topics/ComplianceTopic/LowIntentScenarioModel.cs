using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Model for low-intent scenario user choices.
    /// Maps to LowIntentScenarioCard input fields.
    /// </summary>
    public class LowIntentScenarioModel : BaseCardModel
    {
        [JsonPropertyName("user_choice")]
        [Required(ErrorMessage = "Please select an option to continue")]
        public string? UserChoice { get; set; }

        // Computed properties for business logic
        public bool WantsBasics => UserChoice == "insurance_basics";
        public bool WantsCoverageGuides => UserChoice == "coverage_guides";
        public bool WantsCalculators => UserChoice == "calculators";
        public bool WantsToLeave => UserChoice == "go_home";

        public override string ToString()
        {
            return $"LowIntentChoice: {UserChoice}";
        }
    }
}