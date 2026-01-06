namespace InsuranceSemanticV2.Core.DTO;

public class SetStatusRequest
{
    public string Status { get; set; } = string.Empty;  // Online, Away, OnCall, Offline
}
