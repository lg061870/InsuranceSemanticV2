using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Model for medium-intent scenario user choices.
    /// Maps to MediumIntentScenarioCard input fields.
    /// </summary>
    public class MediumIntentScenarioModel : BaseCardModel
    {
        [JsonPropertyName("user_choice")]
        [Required(ErrorMessage = "Please select an option to continue")]
        public string? UserChoice { get; set; }

        // Computed properties for business logic
        public bool WantsEducationalContent => UserChoice == "view_guides";
        public bool WantsAgentChat => UserChoice == "chat_agent";
        public bool WantsCallback => UserChoice == "schedule_callback";
        public bool WantsEmailOnly => UserChoice == "email_info";
        public bool WantsToLeave => UserChoice == "go_home";

        public override string ToString()
        {
            return $"MediumIntentChoice: {UserChoice}";
        }
    }
}