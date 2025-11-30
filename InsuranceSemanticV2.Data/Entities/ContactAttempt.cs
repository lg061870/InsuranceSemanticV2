namespace InsuranceSemanticV2.Data.Entities;

public class ContactAttempt {
    public int AttemptId { get; set; }
    public int LeadId { get; set; }

    public DateTime AttemptTime { get; set; }
    public string Method { get; set; }
    public string Outcome { get; set; }

    public Lead Lead { get; set; }
}
