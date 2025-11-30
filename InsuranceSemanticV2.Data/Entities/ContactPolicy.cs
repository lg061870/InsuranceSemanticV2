namespace InsuranceSemanticV2.Data.Entities;

// --------------------------------------------------------
// 3. ContactPolicy – State-based regulatory rules
// --------------------------------------------------------
public class ContactPolicy {
    public int ContactPolicyId { get; set; }
    public string State { get; set; } = string.Empty;
    public int MaxAttemptsPerDay { get; set; }
    public TimeSpan AllowedStartTime { get; set; }
    public TimeSpan AllowedEndTime { get; set; }
    public string? Notes { get; set; }
}
