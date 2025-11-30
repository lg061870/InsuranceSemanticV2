namespace InsuranceSemanticV2.Data.Entities;

// ------------------ CoverageIntent ------------------

public class CoverageIntent {
    public int CoverageIntentId { get; set; }
    public int ProfileId { get; set; }

    public string CoverageType { get; set; }
    public string CoverageStartTime { get; set; }
    public string CoverageAmount { get; set; }
    public string MonthlyBudget { get; set; }

    public LeadProfile Profile { get; set; }
}
