// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class CoverageIntentResponse : BaseResponse<CoverageIntentRequest> {
    public int CoverageintentId { get; set; }
}
