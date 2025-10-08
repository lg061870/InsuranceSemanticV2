using System;
using System.Text.Json.Serialization;

namespace InsuranceAgent.Models {
    /// <summary>
    /// Model for insurance planning intent information
    /// </summary>
    public class PlanningIntentModel
    {
        /// <summary>
        /// Gets or sets the type of insurance coverage intent
        /// </summary>
        [JsonPropertyName("intent_type")]
        public string? IntentType { get; set; }
        
        /// <summary>
        /// Gets or sets the coverage start timeframe
        /// </summary>
        [JsonPropertyName("coverage_duration")]
        public string? CoverageDuration { get; set; }
        
        /// <summary>
        /// Gets or sets the desired coverage amount band
        /// </summary>
        [JsonPropertyName("desired_coverage_amount_band")]
        public string? DesiredCoverageAmountBand { get; set; }
    }
}