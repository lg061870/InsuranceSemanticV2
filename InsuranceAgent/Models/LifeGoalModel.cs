//using System;
//using System.Text.Json.Serialization;

//namespace InsuranceAgent.Models {
//    /// <summary>
//    /// Model for life insurance goal selection data
//    /// </summary>
//    public class LifeGoalModel
//    {
//        /// <summary>
//        /// Gets or sets a value indicating whether the user wants to protect loved ones
//        /// </summary>
//        [JsonPropertyName("intent_protect_loved_ones")]
//        public bool IntentProtectLovedOnes { get; set; }
        
//        /// <summary>
//        /// Gets or sets a value indicating whether the user wants to pay off mortgage
//        /// </summary>
//        [JsonPropertyName("intent_pay_mortgage")]
//        public bool IntentPayMortgage { get; set; }
        
//        /// <summary>
//        /// Gets or sets a value indicating whether the user wants to prepare for family's future
//        /// </summary>
//        [JsonPropertyName("intent_prepare_future")]
//        public bool IntentPrepareFuture { get; set; }
        
//        /// <summary>
//        /// Gets or sets a value indicating whether the user is looking for peace of mind
//        /// </summary>
//        [JsonPropertyName("intent_peace_of_mind")]
//        public bool IntentPeaceOfMind { get; set; }
        
//        /// <summary>
//        /// Gets or sets a value indicating whether the user wants to cover final expenses
//        /// </summary>
//        [JsonPropertyName("intent_cover_expenses")]
//        public bool IntentCoverExpenses { get; set; }
        
//        /// <summary>
//        /// Gets or sets a value indicating whether the user is unsure about their life insurance goals
//        /// </summary>
//        [JsonPropertyName("intent_unsure")]
//        public bool IntentUnsure { get; set; }
//    }
//}