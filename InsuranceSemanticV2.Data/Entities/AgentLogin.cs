namespace InsuranceSemanticV2.Data.Entities;

// --------------------------------------------------------
// 8. AgentLogin – MVP authentication for agents
// --------------------------------------------------------
public class AgentLogin {
    public int AgentLoginId { get; set; }
    public int AgentId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public DateTime LastLogin { get; set; }
    public bool IsActive { get; set; } = true;

    public Agent? Agent { get; set; }
}
