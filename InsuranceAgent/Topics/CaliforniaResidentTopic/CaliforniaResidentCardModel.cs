using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ConversaCore.Cards;

namespace InsuranceAgent.Topics; 
/// <summary>
/// Model representing ZIP code collection and CCPA acknowledgment for California residents.
/// </summary>
public class CaliforniaResidentModel : BaseCardModel {
    [Required(ErrorMessage = "ZIP code is required.")]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "ZIP code must be 5 digits.")]
    [JsonPropertyName("zip_code")]
    public string? ZipCode { get; set; }

    [JsonPropertyName("ccpa_acknowledgment")]
    public string? CcpaAcknowledged { get; set; }

    /// <summary>
    /// True if the CCPA acknowledgment is affirmative ("yes").
    /// </summary>
    public bool? HasCcpaAcknowledgment => CcpaAcknowledged switch {
        "yes" => true,
        "no" => false,
        _ => null
    };

    /// <summary>
    /// Returns true if the ZIP is a valid California ZIP (90001�96162).
    /// </summary>
    public bool IsCaliforniaZip() {
        if (string.IsNullOrWhiteSpace(ZipCode)) return false;
        return int.TryParse(ZipCode, out var zip) && zip >= 90001 && zip <= 96162;
    }

    public bool IsValid(out List<string> errors) {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ZipCode) || !IsCaliforniaZip())
            errors.Add("A valid California ZIP code is required.");

        if (HasCcpaAcknowledgment != true)
            errors.Add("You must acknowledge the CCPA privacy notice.");

        return errors.Count == 0;
    }
}
