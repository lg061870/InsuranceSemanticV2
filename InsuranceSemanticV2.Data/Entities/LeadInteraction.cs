using InsuranceSemanticV2.Data.Entities;

public class LeadInteraction {
    public int LeadInteractionId { get; set; }
    public int LeadId { get; set; }

    // null = client initiated
    public int? AgentId { get; set; }

    // "agent_to_client" or "client_to_agent"
    public string Direction { get; set; } = string.Empty;

    // "call", "sms", "email", "card_submit", etc.
    public string InteractionType { get; set; } = string.Empty;

    // optional structured data (text, form answers, attachments, etc)
    public string? PayloadJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public Lead? Lead { get; set; }
    public Agent? Agent { get; set; }
}
