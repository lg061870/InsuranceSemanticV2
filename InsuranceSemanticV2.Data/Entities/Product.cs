using InsuranceSemanticV2.Data.Entities;

namespace InsuranceSemanticV2.Core.Entities;

public class Product {
    public int ProductId { get; set; }
    public int CarrierId { get; set; }

    public string Name { get; set; }
    public string Type { get; set; }

    public Carrier Carrier { get; set; }
    public List<ProductStateAvailability> StateAvailability { get; set; } = new();
}
