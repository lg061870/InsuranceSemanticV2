using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class ComplianceEndpoints {
    public static RouteGroupBuilder MapComplianceEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/compliance")
                          .WithTags("Compliance");

        // ============================================================
        // GET compliance by ProfileId
        // ============================================================
        group.MapGet("/profile/{profileId:int}", async (int profileId, AppDbContext db) => {
            var compliance = await db.Compliances
                .FirstOrDefaultAsync(c => c.ProfileId == profileId);

            return compliance is null
                ? Results.NotFound()
                : Results.Ok(compliance);
        });

        // ============================================================
        // GET compliance by State
        // ============================================================
        group.MapGet("/state/{state}", async (string state, AppDbContext db) => {
            var list = await db.Compliances
                .Where(c => c.State == state)
                .ToListAsync();

            return Results.Ok(list);
        });

        // ============================================================
        // GET compliance by ZIP
        // ============================================================
        group.MapGet("/zip/{zipCode}", async (string zipCode, AppDbContext db) => {
            var list = await db.Compliances
                .Where(c => c.ZipCode == zipCode)
                .ToListAsync();

            return Results.Ok(list);
        });

        // ============================================================
        // UPSERT compliance for a Profile
        // ============================================================
        group.MapPut("/profile/{profileId:int}", async (int profileId, Compliance dto, AppDbContext db) => {
            var existing = await db.Compliances
                .FirstOrDefaultAsync(c => c.ProfileId == profileId);

            if (existing is null) {
                dto.ProfileId = profileId;
                db.Compliances.Add(dto);
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
