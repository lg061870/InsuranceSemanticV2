namespace InsuranceSemanticV2.Core.DTO;

// ============================================================
//   EMPLOYMENT
// ============================================================

public class EmploymentRequest {
    public int LeadId { get; set; }
    public int ProfileId { get; set; }
    public string EmploymentStatusValue { get; set; }
    public string HouseholdIncomeValue { get; set; }
    public string Occupation { get; set; }
    public string YearsEmployedValue { get; set; }
}
