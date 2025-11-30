// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class ContactHealthResponse : BaseResponse<ContactHealthRequest> {
    public int ContacthealthId { get; set; }
}
