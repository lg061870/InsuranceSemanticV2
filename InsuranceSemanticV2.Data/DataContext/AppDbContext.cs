using InsuranceSemanticV2.Core.Entities;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Data.DataContext; 
public class AppDbContext : DbContext {
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) {
    }

    // ==========================
    //  DbSets (add gradually)
    // ==========================

    // Organization & Users
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentLicense> AgentLicenses => Set<AgentLicense>();
    public DbSet<AgentCarrierAppointment> AgentCarrierAppointments => Set<AgentCarrierAppointment>();
    public DbSet<AgentSession> AgentSessions => Set<AgentSession>();
    public DbSet<AgentLogin> AgentLogins => Set<AgentLogin>();
    public DbSet<AgentAvailability> AgentAvailabilities => Set<AgentAvailability>();

    // Lead Aggregate
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadProfile> LeadProfiles => Set<LeadProfile>();
    public DbSet<LeadStatusHistory> LeadStatusHistories => Set<LeadStatusHistory>();
    public DbSet<LeadScore> LeadScores => Set<LeadScore>();
    public DbSet<LeadInteraction> LeadInteractions => Set<LeadInteraction>();
    public DbSet<LeadAuditLog> LeadAuditLogs => Set<LeadAuditLog>();
    public DbSet<LeadCallback> LeadCallbacks => Set<LeadCallback>();

    // Profile Sub-Models
    public DbSet<ContactInfo> ContactInfos => Set<ContactInfo>();
    public DbSet<Dependents> Dependents => Set<Dependents>();
    public DbSet<Employment> Employments => Set<Employment>();
    public DbSet<InsuranceContext> InsuranceContexts => Set<InsuranceContext>();
    public DbSet<LifeGoals> LifeGoals => Set<LifeGoals>();
    public DbSet<HealthInfo> HealthInfos => Set<HealthInfo>();
    public DbSet<ContactHealth> ContactHealths => Set<ContactHealth>();
    public DbSet<CoverageIntent> CoverageIntents => Set<CoverageIntent>();
    public DbSet<Compliance> Compliances => Set<Compliance>();
    public DbSet<CaliforniaResident> CaliforniaResidents => Set<CaliforniaResident>();
    public DbSet<BeneficiaryInfo> BeneficiaryInfos => Set<BeneficiaryInfo>();
    public DbSet<AssetsLiabilities> AssetsLiabilities => Set<AssetsLiabilities>();

    // Carriers & Products
    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductStateAvailability> ProductStateAvailabilities => Set<ProductStateAvailability>();
    public DbSet<CarrierStateCompliance> CarrierStateCompliances => Set<CarrierStateCompliance>();

    // Scheduling
    public DbSet<LeadAppointment> LeadAppointments => Set<LeadAppointment>();
    public DbSet<LeadFollowUp> LeadFollowUps => Set<LeadFollowUp>();
    public DbSet<ContactAttempt> ContactAttempts => Set<ContactAttempt>();

    // Contact Policies
    public DbSet<ContactPolicy> ContactPolicies => Set<ContactPolicy>();

    // ==========================
    //  Model Building
    // ==========================

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        // Automatically apply all IEntityTypeConfiguration<T> in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}