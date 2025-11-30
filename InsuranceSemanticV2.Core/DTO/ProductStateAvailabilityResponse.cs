// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class ProductStateAvailabilityResponse : BaseResponse<ProductStateAvailabilityRequest> {
    public int AvailabilityId { get; set; }
}
