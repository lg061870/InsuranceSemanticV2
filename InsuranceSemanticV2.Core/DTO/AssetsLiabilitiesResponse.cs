// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class AssetsLiabilitiesResponse : BaseResponse<AssetsLiabilitiesRequest> {
    public int AssetsId { get; set; }
}
