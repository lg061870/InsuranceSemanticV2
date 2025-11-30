namespace InsuranceSemanticV2.Data.Entities;

// ------------------ Compliance ------------------

public class Compliance {
    public int ComplianceId { get; set; }
    public int ProfileId { get; set; }

    public string TcpaConsent { get; set; }
    public string ZipCode { get; set; }
    public string? State { get; set; }

    public LeadProfile Profile { get; set; }
}
