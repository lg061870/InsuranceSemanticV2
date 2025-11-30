using InsuranceSemanticV2.Core.Entities;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceSemanticV2.Data.EntityConfigurations;

/// <summary>
/// Configures one-to-one relationships for LeadProfile and its sub-models
/// Only configures the dependent side to avoid shadow properties
/// </summary>
public class LeadProfileConfiguration : IEntityTypeConfiguration<LeadProfile>
{
    public void Configure(EntityTypeBuilder<LeadProfile> builder)
    {
        // Primary key
        builder.HasKey(p => p.ProfileId);

        // One-to-one with Lead (LeadProfile is dependent)
        builder.HasOne(p => p.Lead)
            .WithOne(l => l.Profile)
            .HasForeignKey<LeadProfile>(p => p.LeadId);

        // One-to-one relationships with sub-models (sub-models are dependent)
        builder.HasOne(p => p.ContactInfo)
            .WithOne(c => c.Profile)
            .HasForeignKey<ContactInfo>(c => c.ProfileId);

        builder.HasOne(p => p.Dependents)
            .WithOne(d => d.Profile)
            .HasForeignKey<Dependents>(d => d.ProfileId);

        builder.HasOne(p => p.Employment)
            .WithOne(e => e.Profile)
            .HasForeignKey<Employment>(e => e.ProfileId);

        builder.HasOne(p => p.InsuranceContext)
            .WithOne(i => i.Profile)
            .HasForeignKey<InsuranceContext>(i => i.ProfileId);

        builder.HasOne(p => p.LifeGoals)
            .WithOne(l => l.Profile)
            .HasForeignKey<LifeGoals>(l => l.ProfileId);

        builder.HasOne(p => p.HealthInfo)
            .WithOne(h => h.Profile)
            .HasForeignKey<HealthInfo>(h => h.ProfileId);

        builder.HasOne(p => p.ContactHealth)
            .WithOne(c => c.Profile)
            .HasForeignKey<ContactHealth>(c => c.ProfileId);

        builder.HasOne(p => p.CoverageIntent)
            .WithOne(c => c.Profile)
            .HasForeignKey<CoverageIntent>(c => c.ProfileId);

        builder.HasOne(p => p.Compliance)
            .WithOne(c => c.Profile)
            .HasForeignKey<Compliance>(c => c.ProfileId);

        builder.HasOne(p => p.CaliforniaResident)
            .WithOne(c => c.Profile)
            .HasForeignKey<CaliforniaResident>(c => c.ProfileId);

        builder.HasOne(p => p.BeneficiaryInfo)
            .WithOne(b => b.Profile)
            .HasForeignKey<BeneficiaryInfo>(b => b.ProfileId);

        builder.HasOne(p => p.AssetsLiabilities)
            .WithOne(a => a.Profile)
            .HasForeignKey<AssetsLiabilities>(a => a.ProfileId);
    }
}
