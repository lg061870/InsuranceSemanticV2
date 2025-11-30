// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   CARRIER
// ============================================================

public class CarrierRequest {
    public string Name { get; set; }
    public string State { get; set; }   // 2-letter state code
}
