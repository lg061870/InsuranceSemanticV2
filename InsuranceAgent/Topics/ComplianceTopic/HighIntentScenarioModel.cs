using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Model for high-intent scenario user choices.
    /// Maps to HighIntentScenarioCard input fields.
    /// </summary>
    public class HighIntentScenarioModel : BaseCardModel
    {
        [JsonPropertyName("user_choice")]
        [Required(ErrorMessage = "Please select an option to continue")]
        public string? UserChoice { get; set; }

        // Computed properties for business logic
        public bool WantsAgentContact => UserChoice == "connect_agent";
        public bool WantsCallback => UserChoice == "schedule_callback";
        public bool WantsToBrowse => UserChoice == "browse_info";
        public bool WantsToLeave => UserChoice == "go_home";

        public override string ToString()
        {
            return $"HighIntentChoice: {UserChoice}";
        }
    }
}