namespace InsuranceSemanticV2.Core.DTO;

public class LeadStatusHistoryRequest {
    public string NewStatus { get; set; } = string.Empty;
    public int? ChangedByAgentId { get; set; }
}
