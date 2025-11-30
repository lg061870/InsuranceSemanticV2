namespace InsuranceSemanticV2.Core.DTO;

public class AgentRequest {
    public int CompanyId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // -------------------------
    //  LIVE AGENT UI ADDITIONS
    // -------------------------

    public string AvatarUrl { get; set; } = "";
    public string Specialty { get; set; } = "";

    public double Rating { get; set; } = 5.0;     // example default
    public int Calls { get; set; } = 0;
    public int AvgMinutes { get; set; } = 5;

    public bool IsAvailable { get; set; } = true;
    public string StatusLabel { get; set; } = "Available Now";
    public string StatusColor { get; set; } = "green";
}
