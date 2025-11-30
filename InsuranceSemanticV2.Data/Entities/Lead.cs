namespace InsuranceSemanticV2.Data.Entities;

public class Lead {
    public int LeadId { get; set; }

    // ---- Core Fields ----
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Status { get; set; } = "new";
    public int? AssignedAgentId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ---- Extended Fields (from LeadDetailsModel → LeadRequest) ----
    public string? LeadSource { get; set; }
    public string? Language { get; set; }
    public string? LeadIntent { get; set; }
    public string? InterestLevel { get; set; }
    public int? QualificationScore { get; set; }
    public bool? FollowUpRequired { get; set; }
    public DateTime? AppointmentDateTime { get; set; }
    public string? LeadUrl { get; set; }
    public string? Notes { get; set; }

    // ---- Navigation Properties ----
    public Agent? AssignedAgent { get; set; }
    public LeadProfile? Profile { get; set; }

    public List<LeadAppointment> Appointments { get; set; } = new();
    public List<LeadFollowUp> FollowUps { get; set; } = new();
    public List<ContactAttempt> ContactAttempts { get; set; } = new();
    public List<LeadInteraction> Interactions { get; set; } = new();
    public List<LeadStatusHistory> StatusHistory { get; set; } = new();
    public List<LeadScore> Scores { get; set; } = new();
    public List<LeadAuditLog> AuditLogs { get; set; } = new();
    public List<LeadCallback> Callbacks { get; set; } = new();
}
