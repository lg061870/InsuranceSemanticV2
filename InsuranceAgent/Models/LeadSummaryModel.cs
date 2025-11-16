using InsuranceAgent.Topics;

namespace InsuranceAgent.Models; 
/// <summary>
/// Aggregates all AdaptiveCard models into a unified structure
/// for SemanticQuery reasoning.
/// </summary>
public class LeadSummaryModel {
    public LeadDetailsModel LeadDetails { get; set; } = new();
    public LifeGoalsModel LifeGoals { get; set; } = new();
    public HealthInfoModel HealthInfo { get; set; } = new();
    public CoverageIntentModel CoverageIntent { get; set; } = new();
    public DependentsModel Dependents { get; set; } = new();
    public EmploymentModel Employment { get; set; } = new();
    public BeneficiaryInfoModel BeneficiaryInfo { get; set; } = new();
    public ContactInfoModel ContactInfo { get; set; } = new();
}
