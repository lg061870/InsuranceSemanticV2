// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class EmploymentResponse : BaseResponse<EmploymentRequest> {
    public int EmploymentId { get; set; }
}
