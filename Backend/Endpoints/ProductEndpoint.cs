using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Middleware.Authorization;
using Backend.Models;
using Backend.Models.Permissions;
using Backend.Models.Product;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Backend.Endpoints;

public class ProductEndpoint
{
    public void Register(RouteGroupBuilder app)
    {
        var group = app.MapGroup("/product");
        group.MapPost("/add", [Authorize] async (
            [FromBody]ProductModel product, 
            ProductDbService productDbService) =>
        {
            var result = await productDbService.AddProductAsync(product);

            return result is not null ? Results.Ok(result.Id.ToString()) : Results.BadRequest();
        }).WithMetadata(new RequiredPermissionAttribute(CustomPermissions.Products.Create));

        group.MapGet("/getAll", async (
            ProductDbService productDbService) =>
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            async IAsyncEnumerable<ProductModel> StreamProducts(IAsyncCursor<ProductModel> cursor)
            {
                while (await cursor.MoveNextAsync())
                    foreach (var product in cursor.Current)
                        yield return product;
            }

            var cursor = await productDbService.GetAllProductsAsync();

            return Results.Stream(async responseStream =>
            {
                await foreach (var product in StreamProducts(cursor))
                    await responseStream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(product, options));
            });
        });

        group.MapGet("/getOne", async (
            ObjectId productId,
            ProductDbService productDbService) =>
        {
            try
            {
                return Results.Ok(await productDbService.GetOneByIdAsync(productId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapGet("/modifyOne", [Authorize] async (
            [FromBody] ProductModel product,
            ObjectId productId,
            HttpContext context,
            ILogger<RoleEndpoint> logger,
            ProductDbService productDbService) =>
        {
            
            if(productId != product.Id)
                return Results.BadRequest("roleId and role do not match.");
        
            var result = await productDbService.UpdateOneAsync(product);

            if (result.Equals(product))
                return Results.BadRequest("Failed to update product. Product is still the same as before the update.");
        
            return Results.Ok(result);
        }).WithMetadata(new RequiredPermissionAttribute(CustomPermissions.Products.Edit));
    }
}