// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class HealthInfoResponse : BaseResponse<HealthInfoRequest> {
    public int HealthinfoId { get; set; }
}
