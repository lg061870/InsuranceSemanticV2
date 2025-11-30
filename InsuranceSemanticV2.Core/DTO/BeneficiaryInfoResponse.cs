// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class BeneficiaryInfoResponse : BaseResponse<BeneficiaryInfoRequest> {
    public int BeneficiaryId { get; set; }
}
