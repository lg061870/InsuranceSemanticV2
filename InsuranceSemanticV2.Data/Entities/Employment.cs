namespace InsuranceSemanticV2.Data.Entities;

// ------------------ Employment ------------------

public class Employment {
    public int EmploymentId { get; set; }
    public int ProfileId { get; set; }

    public string EmploymentStatusValue { get; set; }
    public string HouseholdIncomeValue { get; set; }
    public string Occupation { get; set; }
    public string YearsEmployedValue { get; set; }

    public LeadProfile Profile { get; set; }
}
