using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics.ComplianceTopic
{
    /// <summary>
    /// Model for compliance and consent data collection.
    /// Maps to the ComplianceCard input fields.
    /// </summary>
    public class ComplianceModel : BaseCardModel
    {
        [JsonPropertyName("tcpac_consent")]
        [Required(ErrorMessage = "TCPA consent is required to proceed")]
        public string? TcpaConsent { get; set; }

        [JsonPropertyName("ccpa_acknowledged")]
        [Required(ErrorMessage = "CCPA acknowledgment is required")]
        public string? CcpaAcknowledged { get; set; }

        // Computed properties for easier business logic
        public bool HasTcpaConsent => TcpaConsent == "yes";
        public bool HasCcpaAcknowledgment => CcpaAcknowledged == "yes";

        // Validation method
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (!HasTcpaConsent)
            {
                errors.Add("You must consent to be contacted to proceed with your insurance inquiry.");
            }

            if (!HasCcpaAcknowledgment)
            {
                errors.Add("You must acknowledge the privacy notice to proceed.");
            }

            return errors.Count == 0;
        }

        public override string ToString()
        {
            return $"Compliance: TCPA={TcpaConsent}, CCPA={CcpaAcknowledged}";
        }
    }
}