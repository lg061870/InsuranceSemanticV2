using System.ComponentModel.DataAnnotations;

namespace SimpleBlazorDemo.Models
{
    public class TrustSignalsModel
    {
        // Empty model for display-only card
    }

    public class ContactModel
    {
        [Required]
        public string? Name { get; set; }

        [Required, EmailAddress]
        public string? Email { get; set; }

        [Required, Phone]
        public string? Phone { get; set; }
    }

    public class TcpaModel
    {
        [Required]
        public bool Consent { get; set; }
    }

    public class CcpaModel
    {
        [Required]
        public bool Acknowledged { get; set; }
    }

    public class ApplicabilityModel
    {
        [Required]
        public bool Applicable { get; set; }
    }

    public class InsuranceTypesModel
    {
        // Empty model for display-only card
    }

    public class LightQualModel
    {
        [Required]
        public string? Goals { get; set; }

        [Required]
        public string? Family { get; set; }

        [Required]
        public string? Mortgage { get; set; }
    }

    public class FirstTimerContactModel
    {
        [Required]
        public string? Name { get; set; }

        [Required]
        public string? Zip { get; set; }
    }

    public class FirstTimerConsentModel
    {
        [Required]
        public bool TcpConsent { get; set; }

        // For California residents, this also serves as CCPA acknowledgement
        public bool CcpaAcknowledged { get; set; }
    }
}