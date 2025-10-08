using ConversaCore.Cards;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace InsuranceAgent.Topics.ContactInfoTopic
{
    /// <summary>
    /// Model for contact information form data.
    /// Inherits from BaseCardModel for continuation support.
    /// </summary>
    public class ContactInfoModel : BaseCardModel
    {
        [JsonPropertyName("full_name")]
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("phone_number")]
        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("email_address")]
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string EmailAddress { get; set; } = string.Empty;

        // Contact Time Preferences
        [JsonPropertyName("contact_time_morning")]
        public string ContactTimeMorning { get; set; } = "false";

        [JsonPropertyName("contact_time_afternoon")]
        public string ContactTimeAfternoon { get; set; } = "false";

        [JsonPropertyName("contact_time_evening")]
        public string ContactTimeEvening { get; set; } = "false";

        [JsonPropertyName("contact_time_any")]
        public string ContactTimeAny { get; set; } = "false";

        // Contact Method Preferences
        [JsonPropertyName("contact_method_phone")]
        public string ContactMethodPhone { get; set; } = "false";

        [JsonPropertyName("contact_method_email")]
        public string ContactMethodEmail { get; set; } = "false";

        [JsonPropertyName("contact_method_either")]
        public string ContactMethodEither { get; set; } = "false";

        // Consent
        [JsonPropertyName("consent_contact")]
        [Required(ErrorMessage = "Contact consent is required")]
        public string ConsentContact { get; set; } = "no";

        // Computed properties for business logic
        public bool HasContactConsent => ConsentContact?.ToLower() == "yes";

        // Best Contact Time determination
        public string BestContactTime
        {
            get
            {
                if (ContactTimeMorning?.ToLower() == "true") return "Morning";
                if (ContactTimeAfternoon?.ToLower() == "true") return "Afternoon";
                if (ContactTimeEvening?.ToLower() == "true") return "Evening";
                if (ContactTimeAny?.ToLower() == "true") return "Anytime";
                return "Not specified";
            }
        }

        // Preferred Contact Method determination
        public string PreferredContactMethod
        {
            get
            {
                if (ContactMethodPhone?.ToLower() == "true") return "Phone";
                if (ContactMethodEmail?.ToLower() == "true") return "Email";
                if (ContactMethodEither?.ToLower() == "true") return "Either";
                return "Not specified";
            }
        }

        // Contact time preferences as list
        public List<string> ContactTimePreferences
        {
            get
            {
                var preferences = new List<string>();
                if (ContactTimeMorning?.ToLower() == "true") preferences.Add("Morning");
                if (ContactTimeAfternoon?.ToLower() == "true") preferences.Add("Afternoon");
                if (ContactTimeEvening?.ToLower() == "true") preferences.Add("Evening");
                if (ContactTimeAny?.ToLower() == "true") preferences.Add("Anytime");
                return preferences;
            }
        }

        // Contact method preferences as list
        public List<string> ContactMethodPreferences
        {
            get
            {
                var methods = new List<string>();
                if (ContactMethodPhone?.ToLower() == "true") methods.Add("Phone");
                if (ContactMethodEmail?.ToLower() == "true") methods.Add("Email");
                if (ContactMethodEither?.ToLower() == "true") methods.Add("Either");
                return methods;
            }
        }

        // Validation helper
        public bool IsValidPhoneNumber
        {
            get
            {
                if (string.IsNullOrWhiteSpace(PhoneNumber)) return false;
                // Basic phone number validation (US format)
                var phoneRegex = new Regex(@"^\+?1?[-\s]?\(?([0-9]{3})\)?[-\s]?([0-9]{3})[-\s]?([0-9]{4})$");
                return phoneRegex.IsMatch(PhoneNumber);
            }
        }

        // Validation helper
        public bool IsValidEmailAddress
        {
            get
            {
                if (string.IsNullOrWhiteSpace(EmailAddress)) return false;
                try
                {
                    var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
                    return emailRegex.IsMatch(EmailAddress);
                }
                catch
                {
                    return false;
                }
            }
        }

        // Lead quality scoring
        public int LeadQualityScore
        {
            get
            {
                var score = 0;
                if (!string.IsNullOrWhiteSpace(FullName)) score += 20;
                if (IsValidPhoneNumber) score += 30;
                if (IsValidEmailAddress) score += 30;
                if (HasContactConsent) score += 20;
                return score;
            }
        }

        public string LeadQualityGrade => LeadQualityScore switch
        {
            >= 90 => "A+",
            >= 80 => "A",
            >= 70 => "B",
            >= 60 => "C",
            _ => "D"
        };
    }
}