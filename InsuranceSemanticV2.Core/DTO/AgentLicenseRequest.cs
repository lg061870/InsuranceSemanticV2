namespace InsuranceSemanticV2.Core.DTO;

public class AgentLicenseRequest {
    public int AgentId { get; set; }
    public string State { get; set; }
    public string LicenseNumber { get; set; }
    public DateTime ExpiresOn { get; set; }
}
