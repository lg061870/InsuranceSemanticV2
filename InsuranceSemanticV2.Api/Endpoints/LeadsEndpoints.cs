// LeadsEndpoints.cs
using AutoMapper;
using InsuranceSemanticV2.Api.Hubs;
using InsuranceSemanticV2.Api.Mapping;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;


namespace InsuranceSemanticV2.Api.Endpoints;

public static class LeadsEndpoints {
    public static RouteGroupBuilder MapLeadEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/leads").WithTags("Leads");


        group.MapPost("/", async (LeadRequest req, AppDbContext db, IHubContext<LeadsHub> hubContext) => {
            var entity = req.ToEntity();
            db.Leads.Add(entity);
            await db.SaveChangesAsync();

            await hubContext.Clients.All.SendAsync("LeadCreated", entity.LeadId);
            await hubContext.Clients.All.SendAsync("KpisChanged");

            return Results.Created($"/api/leads/{entity.LeadId}",
            new LeadResponse {
                LeadId = entity.LeadId,
                Payload = new List<LeadRequest> { req }
            });
        });


        group.MapPut("/{leadId:int}", async (int leadId, LeadRequest req, AppDbContext db) => {
            var lead = await db.Leads.FindAsync(leadId);
            if (lead is null) return Results.NotFound();


            db.Entry(lead).CurrentValues.SetValues(req.ToEntity());
            await db.SaveChangesAsync();
            return Results.Ok();
        });


        // NEW: List all leads for LiveAgentConsole with on-demand lifecycle updates
        group.MapGet("/", async (AppDbContext db, IMapper mapper, InsuranceSemanticV2.Api.Services.LeadLifecycleService lifecycleService) => {
            var leads = await db.Leads
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.ContactInfo)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.HealthInfo)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.LifeGoals)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.CoverageIntent)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.Dependents)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.Employment)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.BeneficiaryInfo)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.AssetsLiabilities)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            // Apply on-demand lifecycle status updates (excludes abandoned leads)
            int updatedCount = 0;
            foreach (var lead in leads)
            {
                if (lifecycleService.UpdateLeadStatus(lead))
                {
                    updatedCount++;
                }
            }

            // Save changes if any statuses were updated
            if (updatedCount > 0)
            {
                await db.SaveChangesAsync();
            }

            var leadDtos = leads.Select(l => {
                var dto = mapper.Map<LeadRequest>(l);
                dto.LeadId = l.LeadId; // Ensure LeadId is populated
                // Fallback: if lead doesn't have FullName, try to get it from ContactInfo
                if (string.IsNullOrEmpty(dto.FullName) && l.Profile?.ContactInfo != null) {
                    dto.FullName = l.Profile.ContactInfo.FullName;
                }
                if (string.IsNullOrEmpty(dto.Email) && l.Profile?.ContactInfo != null) {
                    dto.Email = l.Profile.ContactInfo.EmailAddress;
                }
                if (string.IsNullOrEmpty(dto.Phone) && l.Profile?.ContactInfo != null) {
                    dto.Phone = l.Profile.ContactInfo.PhoneNumber;
                }

                // Progress tracking: check which sections are completed
                if (l.Profile != null) {
                    dto.HasContactInfo = l.Profile.ContactInfo != null;
                    dto.HasHealthInfo = l.Profile.HealthInfo != null;
                    dto.HasLifeGoals = l.Profile.LifeGoals != null;
                    dto.HasCoverageIntent = l.Profile.CoverageIntent != null;
                    dto.HasDependents = l.Profile.Dependents != null;
                    dto.HasEmployment = l.Profile.Employment != null;
                    dto.HasBeneficiaryInfo = l.Profile.BeneficiaryInfo != null;
                    dto.HasAssetsLiabilities = l.Profile.AssetsLiabilities != null;
                }

                return dto;
            }).ToList();

            return Results.Ok(new LeadResponse {
                Payload = leadDtos
            });
        });


        group.MapGet("/{leadId:int}", async (int leadId, AppDbContext db, IMapper mapper) => {
            var lead = await db.Leads.FindAsync(leadId);
            if (lead is null) return Results.NotFound();

            var dto = mapper.Map<LeadRequest>(lead);

            return Results.Ok(new LeadResponse {
                LeadId = lead.LeadId,
                Payload = new() { dto }
            });
        });

        // KPI endpoint for dashboard metrics
        group.MapGet("/kpis", async (AppDbContext db) => {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var yesterday = today.AddDays(-1);
            var thisWeek = today.AddDays(-(int)today.DayOfWeek); // Start of week
            
            var allLeads = await db.Leads
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.ContactInfo)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.HealthInfo)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.LifeGoals)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.CoverageIntent)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.Dependents)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.Employment)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.BeneficiaryInfo)
                .Include(l => l.Profile)
                    .ThenInclude(p => p!.AssetsLiabilities)
                .ToListAsync();
            
            var leadsToday = allLeads.Where(l => l.CreatedAt >= today).ToList();
            var leadsYesterday = allLeads.Where(l => l.CreatedAt >= yesterday && l.CreatedAt < today).Count();
            
            // Calculate progress for each lead
            var leadsWithProgress = allLeads.Select(l => new {
                Lead = l,
                Progress = CalculateProgress(l)
            }).ToList();
            
            var kpis = new LeadKpiResponse {
                TotalLeads = allLeads.Count,
                NewLeadsToday = leadsToday.Count,
                NewLeadsThisWeek = allLeads.Count(l => l.CreatedAt >= thisWeek),
                
                // Temperature (based on score)
                HotLeads = allLeads.Count(l => l.QualificationScore >= 80),
                WarmLeads = allLeads.Count(l => l.QualificationScore >= 60 && l.QualificationScore < 80),
                ColdLeads = allLeads.Count(l => l.QualificationScore < 60),
                
                // Status breakdown
                ActiveLeads = allLeads.Count(l => l.Status.ToLower() == "active" || l.Status.ToLower() == "new"),
                OnHoldLeads = allLeads.Count(l => l.Status.ToLower() == "on-hold"),
                ToRescueLeads = allLeads.Count(l => l.Status.ToLower() == "to-rescue"),
                AbandonedLeads = allLeads.Count(l => l.Status.ToLower() == "abandoned"),
                
                // Qualification
                AverageQualificationScore = allLeads.Any(l => l.QualificationScore.HasValue) 
                    ? allLeads.Where(l => l.QualificationScore.HasValue).Average(l => l.QualificationScore!.Value)
                    : 0,
                QualifiedLeads = allLeads.Count(l => l.QualificationScore >= 70),
                UnqualifiedLeads = allLeads.Count(l => l.QualificationScore < 40),
                
                // Progress
                AverageProgressPercentage = leadsWithProgress.Any() 
                    ? leadsWithProgress.Average(l => l.Progress) 
                    : 0,
                CompletedProfiles = leadsWithProgress.Count(l => l.Progress >= 100),
                
                // Time-based
                AverageHoursInSystem = allLeads.Any() 
                    ? allLeads.Average(l => (now - l.CreatedAt).TotalHours)
                    : 0,
                LeadsNeedingFollowUp = allLeads.Count(l => l.FollowUpRequired == true),
                
                // Velocity
                LeadVelocityPercentage = leadsYesterday > 0 
                    ? ((leadsToday.Count - leadsYesterday) / (double)leadsYesterday) * 100
                    : (leadsToday.Count > 0 ? 100 : 0),
                LeadsTodayVsYesterday = leadsToday.Count - leadsYesterday
            };
            
            return Results.Ok(kpis);
        });

        return group;
    }

    // Helper method to calculate lead progress percentage
    private static int CalculateProgress(Lead lead) {
        if (lead.Profile == null) return 0;
        
        int completed = 0;
        int total = 8;
        
        if (lead.Profile.ContactInfo != null) completed++;
        if (lead.Profile.HealthInfo != null) completed++;
        if (lead.Profile.LifeGoals != null) completed++;
        if (lead.Profile.CoverageIntent != null) completed++;
        if (lead.Profile.Dependents != null) completed++;
        if (lead.Profile.Employment != null) completed++;
        if (lead.Profile.BeneficiaryInfo != null) completed++;
        if (lead.Profile.AssetsLiabilities != null) completed++;
        
        return (int)((completed / (double)total) * 100);
    }
}