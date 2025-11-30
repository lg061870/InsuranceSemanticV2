namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   DEPENDENTS
// ============================================================

public class DependentsRequest {
    public int LeadId { get; set; }
    public int ProfileId { get; set; }
    public string MaritalStatusValue { get; set; }
    public string HasDependentsValue { get; set; }
    public int NoOfChildren { get; set; }

    public bool AgeRange0To5 { get; set; }
    public bool AgeRange6To12 { get; set; }
    public bool AgeRange13To17 { get; set; }
    public bool AgeRange18To25 { get; set; }
    public bool AgeRange25Plus { get; set; }
}
