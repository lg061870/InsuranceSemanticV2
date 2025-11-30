using InsuranceSemanticV2.Core.Entities;

namespace InsuranceSemanticV2.Data.Entities;

// =======================================================
// ================= CARRIERS & PRODUCTS =================
// =======================================================

public class Carrier {
    public int CarrierId { get; set; }
    public string Name { get; set; }
    public string State { get; set; }

    public List<Product> Products { get; set; } = new();
    public List<AgentCarrierAppointment> AgentAppointments { get; set; } = new();
    public List<CarrierStateCompliance> StateCompliances { get; set; } = new();
}
