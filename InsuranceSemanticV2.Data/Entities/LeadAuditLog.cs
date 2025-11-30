namespace InsuranceSemanticV2.Data.Entities;

// --------------------------------------------------------
// 9. (Bonus) LeadAuditLog – Optional but recommended
// --------------------------------------------------------
public class LeadAuditLog {
    public int LeadAuditLogId { get; set; }
    public int LeadId { get; set; }
    public string Action { get; set; } = string.Empty;     // "Created", "Updated", "CardSubmitted"
    public string? DetailsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? AgentId { get; set; }

    public Lead? Lead { get; set; }
    public Agent? Agent { get; set; }
}
