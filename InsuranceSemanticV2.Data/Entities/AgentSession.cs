namespace InsuranceSemanticV2.Data.Entities;

// ------------------ AgentSession ------------------
// Tracks active agent sessions for authentication and presence management

public class AgentSession {
    public int AgentSessionId { get; set; }
    public int AgentId { get; set; }

    public string ConnectionId { get; set; } = string.Empty;  // SignalR connection ID
    public DateTime LoginTime { get; set; }
    public DateTime LastActivityTime { get; set; }
    public DateTime? LogoutTime { get; set; }

    public string Status { get; set; } = "Online";  // Online, Away, OnCall, Offline
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Agent? Agent { get; set; }
}
