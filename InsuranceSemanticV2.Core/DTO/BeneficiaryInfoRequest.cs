namespace InsuranceSemanticV2.Core.DTO;


// ============================================================
//   BENEFICIARY INFO
// ============================================================


public class BeneficiaryInfoRequest {
    public int LeadId { get; set; }
    public int ProfileId { get; set; }
    public string BeneficiaryName { get; set; }
    public string BeneficiaryRelationship { get; set; }
    public DateTime BeneficiaryDob { get; set; }
    public int BeneficiaryPercentage { get; set; }
}
