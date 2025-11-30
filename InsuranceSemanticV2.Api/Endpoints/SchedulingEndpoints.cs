using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class SchedulingEndpoints {
    public static RouteGroupBuilder MapSchedulingEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/scheduling")
                          .WithTags("Scheduling");

        // ------------------------------------------------------------
        // CREATE callback request
        // ------------------------------------------------------------
        group.MapPost("/callback/{leadId:int}", async (int leadId, LeadCallback dto, AppDbContext db) => {
            var exists = await db.Leads.AnyAsync(x => x.LeadId == leadId);
            if (!exists) return Results.NotFound($"Lead {leadId} not found.");

            dto.LeadId = leadId;
            dto.CreatedAt = DateTime.UtcNow;
            dto.Status = dto.Status ?? "Pending";

            db.LeadCallbacks.Add(dto);
            await db.SaveChangesAsync();

            return Results.Created($"/api/scheduling/callback/{dto.LeadCallbackId}", dto);
        });

        // ------------------------------------------------------------
        // GET all callback requests for a lead
        // ------------------------------------------------------------
        group.MapGet("/callback/{leadId:int}", async (int leadId, AppDbContext db) => {
            var items = await db.LeadCallbacks
                .Where(c => c.LeadId == leadId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return Results.Ok(items);
        });

        return group;
    }
}
