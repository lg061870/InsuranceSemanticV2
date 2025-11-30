namespace InsuranceSemanticV2.Data.Entities;

// ------------------ InsuranceContext ------------------

public class InsuranceContext {
    public int ContextId { get; set; }
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

    public LeadProfile Profile { get; set; }
}
