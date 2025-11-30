using InsuranceSemanticV2.Core.Entities;

namespace InsuranceSemanticV2.Data.Entities;

// ------------------ LeadProfile (Root of Sub-Models) ------------------

public class LeadProfile {
    public int ProfileId { get; set; }
    public int LeadId { get; set; }

    public Lead Lead { get; set; }

    public ContactInfo ContactInfo { get; set; }
    public Dependents Dependents { get; set; }
    public Employment Employment { get; set; }
    public InsuranceContext InsuranceContext { get; set; }
    public LifeGoals LifeGoals { get; set; }
    public HealthInfo HealthInfo { get; set; }
    public ContactHealth ContactHealth { get; set; }
    public CoverageIntent CoverageIntent { get; set; }
    public Compliance Compliance { get; set; }
    public CaliforniaResident CaliforniaResident { get; set; }
    public BeneficiaryInfo BeneficiaryInfo { get; set; }
    public AssetsLiabilities AssetsLiabilities { get; set; }
}
