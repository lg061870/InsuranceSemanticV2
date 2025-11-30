namespace InsuranceSemanticV2.Data.Entities;

// --------------------------------------------------------
// 6. CarrierStateCompliance – State-level carrier rules
// --------------------------------------------------------
public class CarrierStateCompliance {
    public int CarrierStateComplianceId { get; set; }
    public int CarrierId { get; set; }
    public string State { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleDescription { get; set; } = string.Empty;

    public Carrier? Carrier { get; set; }
}
