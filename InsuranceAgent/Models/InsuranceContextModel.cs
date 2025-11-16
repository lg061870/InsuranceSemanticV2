//using System;
//using System.Text.Json.Serialization;

//namespace InsuranceAgent.Models {
//    /// <summary>
//    /// Model for insurance context details
//    /// </summary>
//    public class InsuranceContextModel
//    {
//        /// <summary>
//        /// Gets or sets the type of insurance
//        /// </summary>
//        [JsonPropertyName("insurance_type")]
//        public string? InsuranceType { get; set; }
        
//        /// <summary>
//        /// Gets or sets who the coverage is for
//        /// </summary>
//        [JsonPropertyName("coverage_for")]
//        public string? CoverageFor { get; set; }
        
//        /// <summary>
//        /// Gets or sets the coverage goal
//        /// </summary>
//        [JsonPropertyName("coverage_goal")]
//        public string? CoverageGoal { get; set; }
        
//        /// <summary>
//        /// Gets or sets the insurance target amount
//        /// </summary>
//        [JsonPropertyName("insurance_target")]
//        public string? InsuranceTarget { get; set; }
        
//        /// <summary>
//        /// Gets or sets the home value
//        /// </summary>
//        [JsonPropertyName("home_value")]
//        public decimal? HomeValue { get; set; }
        
//        /// <summary>
//        /// Gets or sets the mortgage balance
//        /// </summary>
//        [JsonPropertyName("mortgage_balance")]
//        public decimal? MortgageBalance { get; set; }
        
//        /// <summary>
//        /// Gets or sets the monthly mortgage payment
//        /// </summary>
//        [JsonPropertyName("monthly_mortgage")]
//        public decimal? MonthlyMortgage { get; set; }
        
//        /// <summary>
//        /// Gets or sets the loan term in years
//        /// </summary>
//        [JsonPropertyName("loan_term")]
//        public int? LoanTerm { get; set; }
        
//        /// <summary>
//        /// Gets or sets the equity in property
//        /// </summary>
//        [JsonPropertyName("equity")]
//        public decimal? Equity { get; set; }
        
//        /// <summary>
//        /// Gets or sets a value indicating whether the person has existing life insurance
//        /// </summary>
//        [JsonPropertyName("has_existing_life_insurance")]
//        public bool HasExistingLifeInsurance { get; set; }
        
//        /// <summary>
//        /// Gets or sets the existing life insurance coverage amount
//        /// </summary>
//        [JsonPropertyName("existing_life_insurance_coverage")]
//        public string? ExistingLifeInsuranceCoverage { get; set; }
//    }
//}