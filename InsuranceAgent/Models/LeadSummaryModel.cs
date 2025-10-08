using InsuranceAgent.Topics.BeneficiaryInfoTopic;

namespace InsuranceAgent.Models {
    /// <summary>
    /// Model representing the lead summary data
    /// </summary>
    public class LeadSummaryModel
    {
        /// <summary>
        /// Gets or sets the name of the lead
        /// </summary>
        public string? Name { get; set; }
        
        /// <summary>
        /// Gets or sets the email of the lead
        /// </summary>
        public string? Email { get; set; }
        
        /// <summary>
        /// Gets or sets the phone number of the lead
        /// </summary>
        public string? Phone { get; set; }
        
        /// <summary>
        /// Gets or sets the date of birth of the lead
        /// </summary>
        public string? DateOfBirth { get; set; }
        
        /// <summary>
        /// Gets or sets the compliance and notes data
        /// </summary>
        public ComplianceAndNotesModel ComplianceAndNotes { get; set; } = new ComplianceAndNotesModel();
        
        /// <summary>
        /// Gets or sets the beneficiary information
        /// </summary>
        public BeneficiaryInfoModel BeneficiaryInfo { get; set; } = new BeneficiaryInfoModel();
        
        /// <summary>
        /// Gets or sets the lead information
        /// </summary>
        public LeadInfoModel LeadInfo { get; set; } = new LeadInfoModel();
        
        /// <summary>
        /// Gets or sets the contact and health information
        /// </summary>
        public ContactAndHealthModel ContactAndHealth { get; set; } = new ContactAndHealthModel();
        
        /// <summary>
        /// Gets or sets the planning intent information
        /// </summary>
        public PlanningIntentModel PlanningIntent { get; set; } = new PlanningIntentModel();
        
        /// <summary>
        /// Gets or sets the family and dependents information
        /// </summary>
        public FamilyAndDependentsModel FamilyAndDependents { get; set; } = new FamilyAndDependentsModel();
        
        /// <summary>
        /// Gets or sets the financial information
        /// </summary>
        public FinancialInfoModel FinancialInfo { get; set; } = new FinancialInfoModel();
        
        /// <summary>
        /// Gets or sets the insurance context information
        /// </summary>
        public InsuranceContextModel InsuranceContext { get; set; } = new InsuranceContextModel();
        
        /// <summary>
        /// Gets or sets the life goals information
        /// </summary>
        public LifeGoalModel LifeGoal { get; set; } = new LifeGoalModel();
    }
}