// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class LeadResponse : BaseResponse<LeadRequest> {
    public int LeadId { get; set; }
}
