using InsuranceSemanticV2.Data.Entities;

public class LeadStatusHistory {
    public int LeadStatusHistoryId { get; set; }
    public int LeadId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public int? ChangedByAgentId { get; set; }

    public Lead? Lead { get; set; }
    public Agent? Agent { get; set; }
}
