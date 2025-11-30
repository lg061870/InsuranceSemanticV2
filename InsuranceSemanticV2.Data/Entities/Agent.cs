namespace InsuranceSemanticV2.Data.Entities;

public class Agent {
    public int AgentId { get; set; }
    public int CompanyId { get; set; }

    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // ---------- UI fields ----------
    public string AvatarUrl { get; set; } = "";
    public string Specialty { get; set; } = "";

    public double Rating { get; set; } = 5.0;
    public int Calls { get; set; } = 0;
    public int AvgMinutes { get; set; } = 5;

    public bool IsAvailable { get; set; } = true;
    public string StatusLabel { get; set; } = "Available Now";
    public string StatusColor { get; set; } = "green";

    // Navigation properties
    public Company? Company { get; set; }
    public List<AgentLicense> Licenses { get; set; } = new();
    public List<AgentCarrierAppointment> CarrierAppointments { get; set; } = new();
    public List<AgentSession> Sessions { get; set; } = new();
    public List<AgentLogin> Logins { get; set; } = new();
    public List<AgentAvailability> Availabilities { get; set; } = new();
    public List<Lead> AssignedLeads { get; set; } = new();
    public List<LeadStatusHistory> LeadStatusChanges { get; set; } = new();
    public List<LeadAuditLog> LeadAuditLogs { get; set; } = new();
    public List<LeadInteraction> LeadInteractions { get; set; } = new();
}

