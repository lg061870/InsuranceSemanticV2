// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   PRODUCT
// ============================================================

public class ProductRequest {
    public int CarrierId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }    // Term, Whole, UL, IUL, Final Expense, etc.
}
