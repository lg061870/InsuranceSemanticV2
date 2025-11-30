// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class ContactInfoResponse : BaseResponse<ContactInfoRequest> {
    public int ContactInfoId { get; set; }
}
