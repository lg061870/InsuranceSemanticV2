// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   PRODUCT STATE AVAILABILITY
// ============================================================

public class ProductStateAvailabilityRequest {
    public int ProductId { get; set; }
    public string State { get; set; }
}
