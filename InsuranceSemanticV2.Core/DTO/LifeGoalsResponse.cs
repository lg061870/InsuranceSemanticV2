// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class LifeGoalsResponse : BaseResponse<LifeGoalsRequest> {
    public int LifegoalsId { get; set; }
}
