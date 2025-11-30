namespace InsuranceSemanticV2.Data.Entities;

// --------------------------------------------------------
// 1. LeadCallback – Scheduled callbacks from customer UI
// --------------------------------------------------------
public class LeadCallback {
    public int LeadCallbackId { get; set; }
    public int LeadId { get; set; }
    public DateTime ScheduledDate { get; set; }
    public TimeSpan? ScheduledTime { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Pending";   // Pending, Completed, Cancelled
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Lead? Lead { get; set; }
}
