// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class CarrierResponse : BaseResponse<CarrierRequest> {
    public int CarrierId { get; set; }
}
