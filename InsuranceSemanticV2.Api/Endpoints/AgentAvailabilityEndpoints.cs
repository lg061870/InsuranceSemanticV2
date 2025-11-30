using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class AgentAvailabilityEndpoints {
    public static RouteGroupBuilder MapAgentAvailabilityEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/agent-availability")
                          .WithTags("Agent Availability");

        // GET weekly schedule for an agent
        group.MapGet("/{agentId:int}", async (int agentId, AppDbContext db) => {
            var availability = await db.AgentAvailabilities
                .Where(a => a.AgentId == agentId)
                .OrderBy(a => a.DayOfWeek)
                .ToListAsync();

            return Results.Ok(availability);
        });

        // CREATE or UPDATE availability slot
        group.MapPut("/{agentId:int}", async (int agentId, AgentAvailability dto, AppDbContext db) => {
            var existing = await db.AgentAvailabilities
                .FirstOrDefaultAsync(a =>
                    a.AgentId == agentId &&
                    a.DayOfWeek == dto.DayOfWeek);

            if (existing is null) {
                dto.AgentId = agentId;
                db.AgentAvailabilities.Add(dto);
            }
            else {
                db.Entry(existing).CurrentValues.SetValues(dto);
            }

            await db.SaveChangesAsync();
            return Results.Ok(dto);
        });

        return group;
    }
}
