namespace InsuranceSemanticV2.Data.Entities;

// ------------------ CaliforniaResident ------------------

public class CaliforniaResident {
    public int CaliforniaResidentId { get; set; }
    public int ProfileId { get; set; }

    public string ZipCode { get; set; }
    public string CcpaAcknowledged { get; set; }

    public LeadProfile Profile { get; set; }
}
