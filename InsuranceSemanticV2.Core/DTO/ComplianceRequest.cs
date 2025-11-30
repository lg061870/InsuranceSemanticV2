namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   COMPLIANCE
// ============================================================

public class ComplianceRequest {
    public int ProfileId { get; set; }
    public string TcpaConsent { get; set; }       // "Yes" / "No"
    public string ZipCode { get; set; }
}
