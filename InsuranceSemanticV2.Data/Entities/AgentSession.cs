namespace InsuranceSemanticV2.Data.Entities;

// ------------------ AgentSession ------------------

public class AgentSession {
    public int SessionId { get; set; }
    public int AgentId { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public Agent Agent { get; set; }
}
