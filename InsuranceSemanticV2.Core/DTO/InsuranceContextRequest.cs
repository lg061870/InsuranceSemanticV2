// ============================================================
//   BASE RESPONSE (Option A: Generic Envelope)
// ============================================================

namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   INSURANCE CONTEXT
// ============================================================

public class InsuranceContextRequest {
    public int ProfileId { get; set; }
    public string InsuranceType { get; set; }
    public string CoverageFor { get; set; }
    public string CoverageGoal { get; set; }
    public string InsuranceTarget { get; set; }
    public string HomeValueString { get; set; }
    public string MortgageBalanceString { get; set; }
    public string MonthlyMortgageString { get; set; }
    public string LoanTermString { get; set; }
    public string EquityString { get; set; }
    public string HasExistingLifeInsuranceString { get; set; }
    public string ExistingLifeInsuranceCoverage { get; set; }
}
