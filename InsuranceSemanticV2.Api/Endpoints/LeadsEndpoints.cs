// LeadsEndpoints.cs
using AutoMapper;
using InsuranceSemanticV2.Api.Mapping;
using InsuranceSemanticV2.Core.DTO;
using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;


namespace InsuranceSemanticV2.Api.Endpoints;

public static class LeadsEndpoints {
    public static RouteGroupBuilder MapLeadEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/leads").WithTags("Leads");


        group.MapPost("/", async (LeadRequest req, AppDbContext db) => {
            var entity = req.ToEntity();
            db.Leads.Add(entity);
            await db.SaveChangesAsync();


            return Results.Created($"/api/leads/{entity.LeadId}",
            new LeadResponse {
                LeadId = entity.LeadId,
                Payload = new List<LeadRequest> { req }
            });
        });


        group.MapPut("/{leadId:int}", async (int leadId, LeadRequest req, AppDbContext db) => {
            var lead = await db.Leads.FindAsync(leadId);
            if (lead is null) return Results.NotFound();


            db.Entry(lead).CurrentValues.SetValues(req.ToEntity());
            await db.SaveChangesAsync();
            return Results.Ok();
        });


        group.MapGet("/{leadId:int}", async (int leadId, AppDbContext db, IMapper mapper) => {
            var lead = await db.Leads.FindAsync(leadId);
            if (lead is null) return Results.NotFound();

            var dto = mapper.Map<LeadRequest>(lead);

            return Results.Ok(new LeadResponse {
                LeadId = lead.LeadId,
                Payload = new() { dto }
            });
        });



        return group;
    }
}