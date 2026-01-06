namespace InsuranceSemanticV2.Core.DTO;

public class SessionStartRequest
{
    public string ConnectionId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class SessionStartResponse
{
    public int SessionId { get; set; }
}
