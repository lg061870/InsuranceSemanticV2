namespace InsuranceSemanticV2.Core.DTO;

public class ContactAttemptRequest {
    public int LeadId { get; set; }
    public DateTime AttemptTime { get; set; }
    public string Method { get; set; }
    public string Outcome { get; set; }
}
