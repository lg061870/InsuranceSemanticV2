// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class ComplianceResponse : BaseResponse<ComplianceRequest> {
    public int ComplianceId { get; set; }
}
