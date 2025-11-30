namespace InsuranceSemanticV2.Core.DTO;

public class LeadInteractionRequest {
    public int? AgentId { get; set; } // null → client-originated
    public string Direction { get; set; } = string.Empty;
    public string InteractionType { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
}

