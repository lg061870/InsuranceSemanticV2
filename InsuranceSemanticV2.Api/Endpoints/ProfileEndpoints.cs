// ProfileEndpoints.cs
using AutoMapper;
using InsuranceSemanticV2.Api.Hubs;
using InsuranceSemanticV2.Api.Mapping;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Core.Entities;
using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class ProfileEndpoints {
    public static RouteGroupBuilder MapProfileEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/profile").WithTags("Profile");

        static async Task<LeadProfile?> GetOrCreateProfileAsync(int leadId, AppDbContext db) {
            var lead = await db.Leads.Include(x => x.Profile).FirstOrDefaultAsync(x => x.LeadId == leadId);
            if (lead is null) return null;

            if (lead.Profile is null) {
                lead.Profile = new LeadProfile { LeadId = leadId };
                db.LeadProfiles.Add(lead.Profile);
                await db.SaveChangesAsync();
            }

            return lead.Profile;
        }

        group.MapPut("/{leadId:int}/contact-info", async (int leadId, ContactInfoRequest dto, AppDbContext db, IHubContext<LeadsHub> hubContext) => {
            var profile = await GetOrCreateProfileAsync(leadId, db);
            if (profile is null) return Results.NotFound();

            // Check for duplicate: same email AND birthday (but allow updating the same lead)
            if (!string.IsNullOrWhiteSpace(dto.EmailAddress) && dto.DateOfBirth.HasValue) {
                var duplicate = await db.ContactInfos
                    .Include(c => c.Profile)
                        .ThenInclude(p => p.Lead)
                    .FirstOrDefaultAsync(c =>
                        c.EmailAddress == dto.EmailAddress &&
                        c.DateOfBirth == dto.DateOfBirth &&
                        c.Profile.LeadId != leadId); // Exclude current lead

                if (duplicate != null) {
                    return Results.BadRequest(new ContactInfoResponse {
                        Payload = new() { dto },
                        Errors = new List<string> {
                            $"A lead with the same email address ({dto.EmailAddress}) and birthday ({dto.DateOfBirth:yyyy-MM-dd}) already exists in our database."
                        }
                    });
                }
            }

            var entity = dto.ToEntity(profile.ProfileId);
            if (profile.ContactInfo is null) db.ContactInfos.Add(entity);
            else db.Entry(profile.ContactInfo).CurrentValues.SetValues(entity);

            await db.SaveChangesAsync();
            
            // Broadcast profile update via SignalR
            await hubContext.Clients.All.SendAsync("ProfileUpdated", leadId);
            await hubContext.Clients.All.SendAsync("KpisChanged");
            
            return Results.Ok(new ContactInfoResponse { Payload = new() { dto } });
        });

        group.MapPut("/{leadId:int}/health", async (int leadId, HealthInfoRequest dto, AppDbContext db, IHubContext<LeadsHub> hubContext) => {
            var profile = await GetOrCreateProfileAsync(leadId, db);
            if (profile is null) return Results.NotFound();

            var entity = dto.ToEntity(profile.ProfileId);
            if (profile.HealthInfo is null) db.HealthInfos.Add(entity);
            else db.Entry(profile.HealthInfo).CurrentValues.SetValues(entity);

            await db.SaveChangesAsync();
            
            await hubContext.Clients.All.SendAsync("ProfileUpdated", leadId);
            await hubContext.Clients.All.SendAsync("KpisChanged");
            
            return Results.Ok(new HealthInfoResponse { Payload = new() { dto } });
        });

        group.MapPut("/{leadId:int}/goals", async (int leadId, LifeGoalsRequest dto, AppDbContext db, IMapper mapper, IHubContext<LeadsHub> hubContext) => {
            var profile = await GetOrCreateProfileAsync(leadId, db);
            if (profile is null) return Results.NotFound();

            var entity = mapper.Map<LifeGoals>(dto);
            entity.ProfileId = profile.ProfileId;

            if (profile.LifeGoals is null)
                db.LifeGoals.Add(entity);
            else
                db.Entry(profile.LifeGoals).CurrentValues.SetValues(entity);

            await db.SaveChangesAsync();
            
            await hubContext.Clients.All.SendAsync("ProfileUpdated", leadId);
            await hubContext.Clients.All.SendAsync("KpisChanged");
            
            return Results.Ok(new LifeGoalsResponse { Payload = new() { dto } });
        });


        group.MapPut("/{leadId:int}/coverage", async (int leadId, CoverageIntentRequest dto, AppDbContext db, IHubContext<LeadsHub> hubContext) => {
            var profile = await GetOrCreateProfileAsync(leadId, db);
            if (profile is null) return Results.NotFound();

            var entity = dto.ToEntity(profile.ProfileId);
            if (profile.CoverageIntent is null) db.CoverageIntents.Add(entity);
            else db.Entry(profile.CoverageIntent).CurrentValues.SetValues(entity);

            await db.SaveChangesAsync();
            
            await hubContext.Clients.All.SendAsync("ProfileUpdated", leadId);
            await hubContext.Clients.All.SendAsync("KpisChanged");
            
            return Results.Ok(new CoverageIntentResponse { Payload = new() { dto } });
        });

        group.MapPut("/{leadId:int}/dependents", async (int leadId, DependentsRequest dto, AppDbContext db, IHubContext<LeadsHub> hubContext) => {
            var profile = await GetOrCreateProfileAsync(leadId, db);
            if (profile is null) return Results.NotFound();

            var entity = dto.ToEntity(profile.ProfileId);
            if (profile.Dependents is null) db.Dependents.Add(entity);
            else db.Entry(profile.Dependents).CurrentValues.SetValues(entity);

            await db.SaveChangesAsync();
            
            await hubContext.Clients.All.SendAsync("ProfileUpdated", leadId);
            await hubContext.Clients.All.SendAsync("KpisChanged");
            
            return Results.Ok(new DependentsResponse { Payload = new() { dto } });
        });

        group.MapPut("/{leadId:int}/employment", async (int leadId, EmploymentRequest dto, AppDbContext db, IHubContext<LeadsHub> hubContext) => {
            var profile = await GetOrCreateProfileAsync(leadId, db);
            if (profile is null) return Results.NotFound();

            var entity = dto.ToEntity(profile.ProfileId);
            if (profile.Employment is null) db.Employments.Add(entity);
            else db.Entry(profile.Employment).CurrentValues.SetValues(entity);

            await db.SaveChangesAsync();
            
            await hubContext.Clients.All.SendAsync("ProfileUpdated", leadId);
            await hubContext.Clients.All.SendAsync("KpisChanged");
            
            return Results.Ok(new EmploymentResponse { Payload = new() { dto } });
        });

        group.MapPut("/{leadId:int}/beneficiary", async (int leadId, BeneficiaryInfoRequest dto, AppDbContext db, IMapper mapper, IHubContext<LeadsHub> hubContext) => {
            var profile = await GetOrCreateProfileAsync(leadId, db);
            if (profile is null) return Results.NotFound();

            var entity = mapper.Map<BeneficiaryInfo>(dto);
            entity.ProfileId = profile.ProfileId;

            if (profile.BeneficiaryInfo is null)
                db.BeneficiaryInfos.Add(entity);
            else
                db.Entry(profile.BeneficiaryInfo).CurrentValues.SetValues(entity);

            await db.SaveChangesAsync();
            
            await hubContext.Clients.All.SendAsync("ProfileUpdated", leadId);
            await hubContext.Clients.All.SendAsync("KpisChanged");
            
            return Results.Ok(new BeneficiaryInfoResponse { Payload = new() { dto } });
        });


        return group;
    }
}
