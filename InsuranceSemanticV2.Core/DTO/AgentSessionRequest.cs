namespace InsuranceSemanticV2.Core.DTO;

public class AgentSessionRequest {
    public int AgentId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
}
