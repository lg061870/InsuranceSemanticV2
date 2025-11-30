// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class InsuranceContextResponse : BaseResponse<InsuranceContextRequest> {
    public int ContextId { get; set; }
}
