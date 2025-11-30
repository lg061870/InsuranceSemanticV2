namespace InsuranceSemanticV2.Data.Entities;

public class AgentLicense {
    public int LicenseId { get; set; }
    public int AgentId { get; set; }

    public string State { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;

    public DateTime ExpiresOn { get; set; }

    // Navigation
    public Agent? Agent { get; set; }
}
