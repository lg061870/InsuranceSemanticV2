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
        public string? TcpaConsent { get; set; }

        [JsonPropertyName("ccpa_acknowledged")]
        public string? CcpaAcknowledged { get; set; }

        // Computed properties for easier business logic
        // Yes = true, No = false, Don't want to answer/null = null
        public bool? HasTcpaConsent => TcpaConsent switch
        {
            "yes" => true,
            "no" => false,
            _ => null  // "prefer_not_to_answer" or null
        };

        public bool? HasCcpaAcknowledgment => CcpaAcknowledged switch
        {
            "yes" => true,
            "no" => false,
            _ => null  // "prefer_not_to_answer" or null
        };

        // Validation method - now allowing "don't want to answer" responses
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            // Optional: You can add business logic here if certain responses are required
            // For now, all responses (yes/no/prefer not to answer) are considered valid
            
            return errors.Count == 0;
        }

        public override string ToString()
        {
            return $"Compliance: TCPA={TcpaConsent}, CCPA={CcpaAcknowledged}";
        }
    }
}