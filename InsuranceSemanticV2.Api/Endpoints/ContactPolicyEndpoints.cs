using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class ContactPolicyEndpoints {
    public static RouteGroupBuilder MapContactPolicyEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/contact-policy")
                          .WithTags("Contact Policy");

        // GET policy for a state
        group.MapGet("/{state}", async (string state, AppDbContext db) => {
            var policy = await db.ContactPolicies
                .FirstOrDefaultAsync(p => p.State == state);

            return policy is null ? Results.NotFound() : Results.Ok(policy);
        });

        // CREATE or UPDATE policy for a state
        group.MapPut("/{state}", async (string state, ContactPolicy dto, AppDbContext db) => {
            var existing = await db.ContactPolicies
                .FirstOrDefaultAsync(p => p.State == state);

            if (existing is null) {
                dto.State = state;
                db.ContactPolicies.Add(dto);
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
