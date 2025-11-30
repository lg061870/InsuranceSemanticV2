// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class CompanyResponse : BaseResponse<CompanyRequest> {
    public int CompanyId { get; set; }
}
