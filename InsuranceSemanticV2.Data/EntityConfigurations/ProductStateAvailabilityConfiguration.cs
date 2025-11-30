using InsuranceSemanticV2.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceSemanticV2.Data.EntityConfigurations;

public class ProductStateAvailabilityConfiguration : IEntityTypeConfiguration<ProductStateAvailability>
{
    public void Configure(EntityTypeBuilder<ProductStateAvailability> builder)
    {
        builder.HasKey(p => p.AvailabilityId);
    }
}
