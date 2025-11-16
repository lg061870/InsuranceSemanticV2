using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Topics; 
/// <summary>
/// Model representing compliance and consent data for the user.
/// Includes TCPA consent and ZIP code validation logic.
/// </summary>
public class ComplianceModel : BaseCardModel {
    // -----------------------------
    // ✅ TCPA CONSENT HANDLING
    // -----------------------------
    private bool? _hasTcpaConsent;

    [JsonPropertyName("tcpa_consent")]
    [Required(ErrorMessage = "TCPA consent is required.")]
    public string? TcpaConsent {
        get => HasTcpaConsent switch {
            true => "yes",
            false => "no",
            _ => null
        };
        set {
            _hasTcpaConsent = value?.ToLower() switch {
                "yes" or "y" or "true" => true,
                "no" or "n" or "false" => false,
                _ => null
            };
        }
    }

    [JsonIgnore]
    public bool? HasTcpaConsent {
        get => _hasTcpaConsent;
        private set => _hasTcpaConsent = value;
    }

    // -----------------------------
    // ✅ ZIP CODE HANDLING
    // -----------------------------
    [JsonPropertyName("zip_code")]
    [Required(ErrorMessage = "ZIP Code is required.")]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "Please enter a valid 5-digit ZIP Code.")]
    public string? ZipCode { get; set; }

    /// <summary>
    /// Determines if the ZIP code belongs to California.
    /// </summary>
    [JsonIgnore]
    public bool IsCaliforniaZip {
        get {
            if (string.IsNullOrWhiteSpace(ZipCode)) return false;
            if (!int.TryParse(ZipCode, out var zip)) return false;
            return zip >= 90001 && zip <= 96162;
        }
    }

    // -----------------------------
    // ✅ UTILITY
    // -----------------------------
    public override string ToString()
        => $"Compliance: TCPA={TcpaConsent}, ZIP={ZipCode}, IsCA={IsCaliforniaZip}";
}
