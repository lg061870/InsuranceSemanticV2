namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   CALIFORNIA RESIDENT COMPLIANCE (CCPA)
// ============================================================

public class CaliforniaResidentRequest {
    public int ProfileId { get; set; }
    public string ZipCode { get; set; }
    public string CcpaAcknowledged { get; set; } // "Yes" / "No"
}
