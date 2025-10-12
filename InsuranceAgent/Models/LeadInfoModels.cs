using System;
using System.Text.Json.Serialization;
using ConversaCore.Validation;

namespace InsuranceAgent.Models {
    /// <summary>
    /// Model for lead information including location and contact details
    /// </summary>
    public class LeadInfoModel
    {
        /// <summary>
        /// Gets or sets a value indicating whether the lead is a California resident
        /// </summary>
        [JsonPropertyName("is_california_resident")]
        [RequiredChoice(ErrorMessage = "You must confirm whether you are a California resident.")]
        public bool? IsCaliforniaResident { get; set; }
        
        /// <summary>
        /// Gets or sets the ZIP code of the lead
        /// </summary>
        [JsonPropertyName("zip_code")]
        public string? ZipCode { get; set; }

        /// <summary>
        /// Gets or sets the full name of the lead
        /// </summary>
        [JsonPropertyName("lead_name")]
        public string? LeadName { get; set; }

        /// <summary>
        /// Gets or sets the phone number of the lead
        /// </summary>
        [JsonPropertyName("phone_number")]
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Gets or sets the email address of the lead
        /// </summary>
        [JsonPropertyName("email_address")]
        public string? EmailAddress { get; set; }

        /// <summary>
        /// Gets or sets the best time to contact the lead (Morning, Afternoon, Evening, or Anytime)
        /// </summary>
        [JsonPropertyName("best_contact_time")]
        public string? BestContactTime { get; set; }

        /// <summary>
        /// Gets or sets the preferred contact method (Phone, Email, or Either)
        /// </summary>
        [JsonPropertyName("contact_method")]
        public string? ContactMethod { get; set; }
        
        /// <summary>
        /// Gets or sets the preferred language of the lead
        /// </summary>
        [JsonPropertyName("language")]
        public string? Language { get; set; }
        
        /// <summary>
        /// Gets or sets the source where the lead came from
        /// </summary>
        [JsonPropertyName("lead_source")]
        public string? LeadSource { get; set; }
        
        /// <summary>
        /// Gets or sets the interest level of the lead (High, Medium, Low)
        /// </summary>
        [JsonPropertyName("interest_level")]
        public string? InterestLevel { get; set; }
        
        /// <summary>
        /// Gets or sets the lead's intent (Learn, Buy, Compare)
        /// </summary>
        [JsonPropertyName("lead_intent")]
        public string? LeadIntent { get; set; }
        
        /// <summary>
        /// Gets or sets the appointment date and time
        /// </summary>
        [JsonPropertyName("appointment_date_time")]
        public string? AppointmentDateTime { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether follow-up is needed
        /// </summary>
        [JsonPropertyName("follow_up_needed")]
        public bool FollowUpNeeded { get; set; }
        
        /// <summary>
        /// Gets or sets additional notes for the sales agent
        /// </summary>
        [JsonPropertyName("notes_for_sales_agent")]
        public string? NotesForSalesAgent { get; set; }
        
        /// <summary>
        /// Gets or sets the URL related to the lead in a CRM system
        /// </summary>
        [JsonPropertyName("lead_url")]
        public string? LeadUrl { get; set; }
    }
}