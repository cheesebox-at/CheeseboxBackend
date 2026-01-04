using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;
using Backend.Models.Product;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Backend.Endpoints;

public class ProductEndpoint
{
    public void Register(RouteGroupBuilder app)
    {
        var group = app.MapGroup("/product");
        
        group.MapPost("/add", async (ProductModel product, ProductDbService db) =>
        {
            var result = await db.AddProductAsync(product);
            return result is not null ? Results.Ok(result) : Results.BadRequest();
        }).RequireAuthorization();

        group.MapGet("/getAll", async (ProductDbService db) =>
        {
            var cursor = await db.GetAllProductsAsync();
            var products = new List<ProductModel>();
            
            while (await cursor.MoveNextAsync())
            {
                products.AddRange(cursor.Current);
            }
            
            return Results.Ok(products);
        }).RequireAuthorization();
        
        group.MapGet("/getAllPublished", async (ProductDbService db) =>
        {
            var products = await db.GetPublishedProductsAsync();
            return Results.Ok(products);
        });

        group.MapGet("/getOne", async (string id, ProductDbService db) =>
        {
            var product = await db.GetProductByIdAsync(id);
            return product is not null ? Results.Ok(product) : Results.NotFound();
        });

        group.MapPost("/modifyOne", async (string id, ProductModel product, ProductDbService db) =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return Results.BadRequest("Invalid product ID");
                
            var success = await db.UpdateProductAsync(objectId, product);
            return success ? Results.Ok() : Results.NotFound();
        }).RequireAuthorization();
        
        group.MapDelete("/delete", async (string id, ProductDbService db) =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return Results.BadRequest("Invalid product ID");
                
            var success = await db.DeleteProductAsync(objectId);
            return success ? Results.Ok() : Results.NotFound();
        }).RequireAuthorization();
        
        group.MapPost("/publish", async (string id, ProductDbService db) =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return Results.BadRequest("Invalid product ID");
                
            var success = await db.PublishProductAsync(objectId);
            return success ? Results.Ok() : Results.NotFound();
        }).RequireAuthorization();
        
        group.MapPost("/unpublish", async (string id, ProductDbService db) =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return Results.BadRequest("Invalid product ID");
                
            var success = await db.UnpublishProductAsync(objectId);
            return success ? Results.Ok() : Results.NotFound();
        }).RequireAuthorization();
        
        group.MapGet("/checkAvailability", async (string productId, DateTime start, DateTime end, ProductDbService productDb, OrderDbService orderDb) =>
        {
            if (!ObjectId.TryParse(productId, out var objectId))
                return Results.BadRequest("Invalid product ID");
            
            var product = await productDb.GetProductByIdAsync(objectId);
            if (product == null)
                return Results.NotFound("Product not found");
            
            var isAvailable = await orderDb.CheckProductAvailabilityAsync(objectId, start, end);
            
            return Results.Ok(new { isAvailable, inStock = product.InStock });
        });
        
        group.MapGet("/getBookedTimeFrames", async (string productId, DateTime? rangeStart, DateTime? rangeEnd, OrderDbService orderDb) =>
        {
            if (!ObjectId.TryParse(productId, out var objectId))
                return Results.BadRequest("Invalid product ID");
            
            var bookedTimeFrames = await orderDb.GetBookedTimeFramesAsync(objectId);
            
            // Filter by date range if provided
            if (rangeStart.HasValue || rangeEnd.HasValue)
            {
                var filtered = bookedTimeFrames.Where(btf =>
                {
                    // Include if booking overlaps with requested range
                    var bookingStart = btf.StartDate;
                    var bookingEnd = btf.EndDate;
                    var requestStart = rangeStart ?? DateTime.MinValue;
                    var requestEnd = rangeEnd ?? DateTime.MaxValue;
                    
                    // Check for overlap: booking overlaps if not (bookingEnd <= requestStart || bookingStart >= requestEnd)
                    return !(bookingEnd <= requestStart || bookingStart >= requestEnd);
                }).ToList();
                
                return Results.Ok(filtered.Select(btf => new { startDate = btf.StartDate, endDate = btf.EndDate }));
            }
            
            return Results.Ok(bookedTimeFrames.Select(btf => new { startDate = btf.StartDate, endDate = btf.EndDate }));
        });
    }
}