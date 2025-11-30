namespace InsuranceSemanticV2.Data.Entities;

public class LeadFollowUp {
    public int FollowUpId { get; set; }
    public int LeadId { get; set; }

    public DateTime FollowUpDate { get; set; }
    public string Method { get; set; }
    public string Result { get; set; }

    public Lead Lead { get; set; }
}
