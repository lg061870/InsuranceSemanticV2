namespace InsuranceSemanticV2.Data.Entities;

public class Company {
    public int CompanyId { get; set; }
    public string Name { get; set; }

    public List<Agent> Agents { get; set; } = new();
}
