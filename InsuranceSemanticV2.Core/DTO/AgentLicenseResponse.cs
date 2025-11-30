namespace InsuranceSemanticV2.Core.DTO;

public class AgentLicenseResponse : BaseResponse<AgentLicenseRequest> {
    public int LicenseId { get; set; }
}
