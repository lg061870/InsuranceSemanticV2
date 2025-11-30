namespace InsuranceSemanticV2.Core.Entities;

public class ProductStateAvailability {
    public int AvailabilityId { get; set; }        // Primary Key
    public int ProductId { get; set; }             // FK to Product

    public string State { get; set; } = string.Empty;

    public Product? Product { get; set; }          // Navigation (nullable)
}

