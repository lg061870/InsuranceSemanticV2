namespace InsuranceSemanticV2.Core.DTO;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public AgentDto Agent { get; set; } = new();
}

public class AgentDto
{
    public int AgentId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
}
