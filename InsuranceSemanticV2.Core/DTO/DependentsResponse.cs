// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class DependentsResponse : BaseResponse<DependentsRequest> {
    public int DependentsId { get; set; }
}
