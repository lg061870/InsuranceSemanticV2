namespace InsuranceSemanticV2.Data.Entities;

// ------------------ BeneficiaryInfo ------------------

public class BeneficiaryInfo {
    public int BeneficiaryId { get; set; }
    public int ProfileId { get; set; }

    public string BeneficiaryName { get; set; }
    public string BeneficiaryRelationship { get; set; }
    public DateTime? BeneficiaryDob { get; set; }
    public int? BeneficiaryPercentage { get; set; }

    public LeadProfile Profile { get; set; }
}
