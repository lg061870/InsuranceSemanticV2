using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Models {
    /// <summary>
    /// Model for contact and health details
    /// </summary>
    public class ContactAndHealthModel
    {
        /// <summary>
        /// Gets or sets the street address
        /// </summary>
        [JsonPropertyName("address")]
        public string? Address { get; set; }
        
        /// <summary>
        /// Gets or sets the city and state
        /// </summary>
        [JsonPropertyName("city_state")]
        public string? CityState { get; set; }
        
        /// <summary>
        /// Gets or sets the date of birth
        /// </summary>
        [JsonPropertyName("dob")]
        public string? DateOfBirth { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the person has been hospitalized in the past 5 years
        /// </summary>
        [JsonPropertyName("hospitalized_past_5_years")]
        public bool HospitalizedPast5Years { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the person is currently taking medications
        /// </summary>
        [JsonPropertyName("currently_taking_medications")]
        public bool CurrentlyTakingMedications { get; set; }
        
        /// <summary>
        /// Gets or sets the list of medications
        /// </summary>
        [JsonPropertyName("medications")]
        public string? Medications { get; set; }
        
        /// <summary>
        /// Gets or sets the list of medical conditions
        /// </summary>
        [JsonPropertyName("medical_conditions")]
        public string? MedicalConditions { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the person has used tobacco in the past 12 months
        /// </summary>
        [JsonPropertyName("tobacco_use_last_12_months")]
        public bool TobaccoUseLast12Months { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the person has health insurance
        /// </summary>
        [JsonPropertyName("has_health_insurance")]
        public bool HasHealthInsurance { get; set; }
        
        /// <summary>
        /// Gets or sets the person's height
        /// </summary>
        [JsonPropertyName("height")]
        public string? Height { get; set; }
        
        /// <summary>
        /// Gets or sets the person's weight
        /// </summary>
        [JsonPropertyName("weight")]
        public string? Weight { get; set; }
    }
}