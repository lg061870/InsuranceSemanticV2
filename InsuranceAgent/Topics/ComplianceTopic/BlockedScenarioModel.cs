using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Model for blocked scenario user choices.
    /// Maps to BlockedScenarioCard input fields.
    /// </summary>
    public class BlockedScenarioModel : BaseCardModel
    {
        [JsonPropertyName("user_choice")]
        [Required(ErrorMessage = "Please select an option to continue")]
        public string? UserChoice { get; set; }

        // Computed properties for business logic
        public bool WantsHomepage => UserChoice == "homepage";
        public bool WantsGeneralInfo => UserChoice == "general_info";
        public bool WantsSiteSearch => UserChoice == "site_search";

        public override string ToString()
        {
            return $"BlockedScenarioChoice: {UserChoice}";
        }
    }
}