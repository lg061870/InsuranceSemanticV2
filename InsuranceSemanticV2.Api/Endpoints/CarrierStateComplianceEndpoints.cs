using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class CarrierStateComplianceEndpoints {
    public static RouteGroupBuilder MapCarrierStateComplianceEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/carrier-state-compliance")
                          .WithTags("Carrier State Compliance");

        // GET all rules for a carrier
        group.MapGet("/carrier/{carrierId:int}", async (int carrierId, AppDbContext db) => {
            var rules = await db.CarrierStateCompliances
                .Where(x => x.CarrierId == carrierId)
                .ToListAsync();

            return Results.Ok(rules);
        });

        // GET all rules for a state
        group.MapGet("/state/{state}", async (string state, AppDbContext db) => {
            var rules = await db.CarrierStateCompliances
                .Where(x => x.State == state)
                .ToListAsync();

            return Results.Ok(rules);
        });

        // CREATE rule
        group.MapPost("/", async (CarrierStateCompliance dto, AppDbContext db) => {
            db.CarrierStateCompliances.Add(dto);
            await db.SaveChangesAsync();
            return Results.Created($"/api/carrier-state-compliance/{dto.CarrierStateComplianceId}", dto);
        });

        return group;
    }
}
