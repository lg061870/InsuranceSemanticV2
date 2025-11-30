using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class AuthEndpoints {
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/auth")
                          .WithTags("Authentication");

        // ------------------------------------------------------------
        // SIMPLE LOGIN (MVP)
        // ------------------------------------------------------------
        group.MapPost("/login", static async (AgentLogin dto, AppDbContext db) => {
            var agent = await db.AgentLogins
                .FirstOrDefaultAsync(x =>
                    x.Username == dto.Username &&
                    x.PasswordHash == dto.PasswordHash);

            return agent is null
                ? Results.Unauthorized()
                : Results.Ok(agent);
        });

        return group;
    }
}
