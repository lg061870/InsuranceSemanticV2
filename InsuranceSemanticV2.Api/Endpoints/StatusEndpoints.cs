using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using InsuranceSemanticV2.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class StatusEndpoints {
    public static RouteGroupBuilder MapStatusEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/status")
                          .WithTags("Lead Status");

        // CHANGE status + WRITE HISTORY
        group.MapPost("/{leadId:int}", async (int leadId, LeadStatusHistory dto, AppDbContext db) => {
            var lead = await db.Leads.FindAsync(leadId);
            if (lead is null)
                return Results.NotFound($"Lead {leadId} not found.");

            // store old status
            dto.OldStatus = lead.Status;
            dto.NewStatus = dto.NewStatus;    // provided by caller
            dto.LeadId = leadId;
            dto.ChangedAt = DateTime.UtcNow;

            // update lead live status
            lead.Status = dto.NewStatus;
            lead.UpdatedAt = DateTime.UtcNow;

            db.LeadStatusHistories.Add(dto);
            await db.SaveChangesAsync();

            return Results.Ok(dto);
        });

        // GET history
        group.MapGet("/{leadId:int}", async (int leadId, AppDbContext db) => {
            var history = await db.LeadStatusHistories
                .Where(h => h.LeadId == leadId)
                .OrderByDescending(h => h.ChangedAt)
                .ToListAsync();

            return Results.Ok(history);
        });

        return group;
    }
}
