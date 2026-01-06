using InsuranceSemanticV2.Core.Entities;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InsuranceSemanticV2.Data.EntityConfigurations;

/// <summary>
/// Configures primary keys for entities that don't follow the EntityNameId convention
/// </summary>
public class AgentCarrierAppointmentConfiguration : IEntityTypeConfiguration<AgentCarrierAppointment>
{
    public void Configure(EntityTypeBuilder<AgentCarrierAppointment> builder)
    {
        builder.HasKey(e => e.AppointmentId);
    }
}

public class ContactAttemptConfiguration : IEntityTypeConfiguration<ContactAttempt>
{
    public void Configure(EntityTypeBuilder<ContactAttempt> builder)
    {
        builder.HasKey(e => e.AttemptId);
    }
}

public class LeadAppointmentConfiguration : IEntityTypeConfiguration<LeadAppointment>
{
    public void Configure(EntityTypeBuilder<LeadAppointment> builder)
    {
        builder.HasKey(e => e.AppointmentId);
    }
}

public class LeadFollowUpConfiguration : IEntityTypeConfiguration<LeadFollowUp>
{
    public void Configure(EntityTypeBuilder<LeadFollowUp> builder)
    {
        builder.HasKey(e => e.FollowUpId);
    }
}

public class AgentSessionConfiguration : IEntityTypeConfiguration<AgentSession>
{
    public void Configure(EntityTypeBuilder<AgentSession> builder)
    {
        // Primary key
        builder.HasKey(s => s.AgentSessionId);

        // Foreign key to Agent
        builder.HasOne(s => s.Agent)
            .WithMany(a => a.Sessions)
            .HasForeignKey(s => s.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        // String length constraints
        builder.Property(s => s.ConnectionId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.IpAddress)
            .HasMaxLength(50);

        builder.Property(s => s.UserAgent)
            .HasMaxLength(500);

        // Required fields
        builder.Property(s => s.LoginTime)
            .IsRequired();

        builder.Property(s => s.LastActivityTime)
            .IsRequired();

        builder.Property(s => s.IsActive)
            .IsRequired();

        // Indexes for performance
        builder.HasIndex(s => s.AgentId)
            .HasDatabaseName("IX_AgentSessions_AgentId");

        builder.HasIndex(s => s.ConnectionId)
            .HasDatabaseName("IX_AgentSessions_ConnectionId");

        builder.HasIndex(s => s.IsActive)
            .HasDatabaseName("IX_AgentSessions_IsActive");
    }
}

public class AgentLicenseConfiguration : IEntityTypeConfiguration<AgentLicense>
{
    public void Configure(EntityTypeBuilder<AgentLicense> builder)
    {
        builder.HasKey(e => e.LicenseId);
    }
}

public class InsuranceContextConfiguration : IEntityTypeConfiguration<InsuranceContext>
{
    public void Configure(EntityTypeBuilder<InsuranceContext> builder)
    {
        builder.HasKey(e => e.ContextId);
    }
}

public class AssetsLiabilitiesConfiguration : IEntityTypeConfiguration<AssetsLiabilities>
{
    public void Configure(EntityTypeBuilder<AssetsLiabilities> builder)
    {
        builder.HasKey(e => e.AssetsId);
    }
}

public class BeneficiaryInfoConfiguration : IEntityTypeConfiguration<BeneficiaryInfo>
{
    public void Configure(EntityTypeBuilder<BeneficiaryInfo> builder)
    {
        builder.HasKey(e => e.BeneficiaryId);
    }
}
