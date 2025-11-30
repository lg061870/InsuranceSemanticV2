using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class InteractionEndpoints {
    public static RouteGroupBuilder MapInteractionEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/interactions")
                          .WithTags("Interactions");

        // CREATE interaction
        group.MapPost("/{leadId:int}", async (int leadId, LeadInteraction dto, AppDbContext db) => {
            var leadExists = await db.Leads.AnyAsync(x => x.LeadId == leadId);
            if (!leadExists) return Results.NotFound($"Lead {leadId} not found.");

            dto.LeadId = leadId;
            dto.CreatedAt = DateTime.UtcNow;

            db.LeadInteractions.Add(dto);
            await db.SaveChangesAsync();

            return Results.Created($"/api/interactions/{dto.LeadInteractionId}", dto);
        });

        // GET interactions for a lead
        group.MapGet("/{leadId:int}", async (int leadId, AppDbContext db) => {
            var interactions = await db.LeadInteractions
                .Where(x => x.LeadId == leadId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Results.Ok(interactions);
        });

        return group;
    }
}
