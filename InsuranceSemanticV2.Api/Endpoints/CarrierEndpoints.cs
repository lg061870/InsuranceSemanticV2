using InsuranceSemanticV2.Core.Entities;
using InsuranceSemanticV2.Data.DataContext;
using InsuranceSemanticV2.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceSemanticV2.Api.Endpoints;

public static class CarrierEndpoints {
    public static RouteGroupBuilder MapCarrierEndpoints(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/carriers")
                          .WithTags("Carriers & Products");

        // ============================================================
        // CARRIERS — CRUD
        // ============================================================

        // GET all carriers
        group.MapGet("/", async (AppDbContext db) => {
            var carriers = await db.Carriers.ToListAsync();
            return Results.Ok(carriers);
        });

        // GET single carrier
        group.MapGet("/{carrierId:int}", async (int carrierId, AppDbContext db) => {
            var carrier = await db.Carriers.FindAsync(carrierId);
            return carrier is null ? Results.NotFound() : Results.Ok(carrier);
        });

        // CREATE carrier
        group.MapPost("/", async (Carrier dto, AppDbContext db) => {
            db.Carriers.Add(dto);
            await db.SaveChangesAsync();
            return Results.Created($"/api/carriers/{dto.CarrierId}", dto);
        });

        // UPDATE carrier
        group.MapPut("/{carrierId:int}", async (int carrierId, Carrier dto, AppDbContext db) => {
            var entity = await db.Carriers.FindAsync(carrierId);
            if (entity is null) return Results.NotFound();

            db.Entry(entity).CurrentValues.SetValues(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity);
        });

        // DELETE carrier
        group.MapDelete("/{carrierId:int}", async (int carrierId, AppDbContext db) => {
            var entity = await db.Carriers.FindAsync(carrierId);
            if (entity is null) return Results.NotFound();

            db.Carriers.Remove(entity);
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "Carrier deleted." });
        });

        // ============================================================
        // PRODUCTS
        // ============================================================

        // GET all products for a carrier
        group.MapGet("/{carrierId:int}/products", async (int carrierId, AppDbContext db) => {
            var products = await db.Products
                .Where(p => p.CarrierId == carrierId)
                .ToListAsync();

            return Results.Ok(products);
        });

        // CREATE product for carrier
        group.MapPost("/{carrierId:int}/products", async (int carrierId, Product dto, AppDbContext db) => {
            dto.CarrierId = carrierId;
            db.Products.Add(dto);
            await db.SaveChangesAsync();

            return Results.Created($"/api/carriers/{carrierId}/products/{dto.ProductId}", dto);
        });

        // GET single product
        group.MapGet("/{carrierId:int}/products/{productId:int}", async (int carrierId, int productId, AppDbContext db) => {
            var product = await db.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId && p.CarrierId == carrierId);

            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        // UPDATE product
        group.MapPut("/{carrierId:int}/products/{productId:int}", async (int carrierId, int productId, Product dto, AppDbContext db) => {
            var entity = await db.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId && p.CarrierId == carrierId);

            if (entity is null) return Results.NotFound();

            db.Entry(entity).CurrentValues.SetValues(dto);
            await db.SaveChangesAsync();

            return Results.Ok(entity);
        });

        // DELETE product
        group.MapDelete("/{carrierId:int}/products/{productId:int}", async (int carrierId, int productId, AppDbContext db) => {
            var entity = await db.Products
                .FirstOrDefaultAsync(p => p.ProductId == productId && p.CarrierId == carrierId);

            if (entity is null) return Results.NotFound();

            db.Products.Remove(entity);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Product deleted." });
        });

        // ============================================================
        // PRODUCT STATE AVAILABILITY
        // ============================================================

        // GET availability for a product
        group.MapGet("/{carrierId:int}/products/{productId:int}/states", async (int carrierId, int productId, AppDbContext db) => {
            var states = await db.ProductStateAvailabilities
                .Where(x => x.ProductId == productId)
                .ToListAsync();

            return Results.Ok(states);
        });

        // ADD availability entry
        group.MapPost("/{carrierId:int}/products/{productId:int}/states", async (int carrierId, int productId, ProductStateAvailability dto, AppDbContext db) => {
            dto.ProductId = productId;
            db.ProductStateAvailabilities.Add(dto);
            await db.SaveChangesAsync();

            return Results.Created($"/api/carriers/{carrierId}/products/{productId}/states/{dto.AvailabilityId}", dto);
        });

        // REMOVE availability entry
        group.MapDelete("/{carrierId:int}/products/{productId:int}/states/{id:int}", async (int carrierId, int productId, int id, AppDbContext db) => {
            var entity = await db.ProductStateAvailabilities.FindAsync(id);
            if (entity is null || entity.ProductId != productId)
                return Results.NotFound();

            db.ProductStateAvailabilities.Remove(entity);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "State availability removed." });
        });

        return group;
    }
}
