using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceSemanticV2.Data.EntityConfigurations;

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        // Configure the LastModifiedByAgent relationship
        // Use NO ACTION to avoid multiple cascade paths
        builder.HasOne(l => l.LastModifiedByAgent)
            .WithMany(a => a.ModifiedLeads)
            .HasForeignKey(l => l.LastModifiedByAgentId)
            .OnDelete(DeleteBehavior.NoAction);  // Prevent cascade delete conflicts

        // Configure the AssignedAgent relationship (should already exist but being explicit)
        builder.HasOne(l => l.AssignedAgent)
            .WithMany(a => a.AssignedLeads)
            .HasForeignKey(l => l.AssignedAgentId)
            .OnDelete(DeleteBehavior.NoAction);  // Prevent cascade delete conflicts
    }
}
