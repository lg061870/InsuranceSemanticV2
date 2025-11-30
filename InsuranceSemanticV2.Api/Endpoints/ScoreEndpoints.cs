using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class ScoreEndpoints {
    public static RouteGroupBuilder MapScoreEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/scores")
                          .WithTags("Lead Scores");

        // GET all scores for a lead
        group.MapGet("/{leadId:int}", async (int leadId, AppDbContext db) => {
            var scores = await db.LeadScores
                .Where(s => s.LeadId == leadId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return Results.Ok(scores);
        });

        // CREATE a score entry
        group.MapPost("/{leadId:int}", async (int leadId, LeadScore dto, AppDbContext db) => {
            var exists = await db.Leads.AnyAsync(x => x.LeadId == leadId);
            if (!exists) return Results.NotFound($"Lead {leadId} not found.");

            dto.LeadId = leadId;
            dto.CreatedAt = DateTime.UtcNow;

            db.LeadScores.Add(dto);
            await db.SaveChangesAsync();

            return Results.Created($"/api/scores/{dto.LeadScoreId}", dto);
        });

        return group;
    }
}



