// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   LIFE GOALS
// ============================================================

public class LifeGoalsRequest {
    public int LeadId { get; set; }
    public int ProfileId { get; set; }
    public string ProtectLovedOnesString { get; set; }
    public string PayMortgageString { get; set; }
    public string PrepareFutureString { get; set; }
    public string PeaceOfMindString { get; set; }
    public string CoverExpensesString { get; set; }
    public string UnsureString { get; set; }
}
