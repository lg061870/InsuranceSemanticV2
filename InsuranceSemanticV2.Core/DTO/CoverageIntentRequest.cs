namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   COVERAGE INTENT
// ============================================================

public class CoverageIntentRequest {
    public int LeadId { get; set; }
    public int ProfileId { get; set; }
    public string CoverageType { get; set; }
    public string CoverageStartTime { get; set; }
    public string CoverageAmount { get; set; }
    public string MonthlyBudget { get; set; }
}
