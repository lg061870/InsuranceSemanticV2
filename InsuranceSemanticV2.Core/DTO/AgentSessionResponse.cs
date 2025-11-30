namespace InsuranceSemanticV2.Core.DTO;

public class AgentSessionResponse : BaseResponse<AgentSessionRequest> {
    public int SessionId { get; set; }
}
