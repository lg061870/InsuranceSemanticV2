// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

public class ProductResponse : BaseResponse<ProductRequest> {
    public int ProductId { get; set; }
}
