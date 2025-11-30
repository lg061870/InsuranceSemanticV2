namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   ASSETS & LIABILITIES
// ============================================================

public class AssetsLiabilitiesRequest {
    public int ProfileId { get; set; }

    public string HasHomeEquityValue { get; set; }   // "Yes"/"No"
    public string HomeEquityAmount { get; set; }
    public string SavingsAmount { get; set; }
    public string InvestmentsAmount { get; set; }
    public string RetirementAmount { get; set; }
    public string CreditCardDebt { get; set; }
    public string StudentLoans { get; set; }
    public string AutoLoans { get; set; }
    public string MortgageDebt { get; set; }
    public string OtherDebt { get; set; }
}
