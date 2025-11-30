// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class CaliforniaResidentResponse : BaseResponse<CaliforniaResidentRequest> {
    public int CaliforniaresidentId { get; set; }
}
