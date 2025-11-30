namespace InsuranceSemanticV2.Core.DTO;

public class LeadRequest {
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public string Status { get; set; } = "New";
    public int? AssignedAgentId { get; set; }

    // Optional system-managed timestamps
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Fields from AdaptiveCard model mapping
    public string? LeadSource { get; set; }
    public string? Language { get; set; }
    public string? LeadIntent { get; set; }
    public string? InterestLevel { get; set; }

    public int? QualificationScore { get; set; }
    public bool? FollowUpRequired { get; set; }
    public DateTime? AppointmentDateTime { get; set; }

    public string? LeadUrl { get; set; }
    public string? Notes { get; set; }
}
