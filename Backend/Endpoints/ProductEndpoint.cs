using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;
using Backend.Models.Product;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Authorization;
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

            return result is not null ? Results.Ok(result.Id.ToString()) : Results.BadRequest();
        }).RequireAuthorization();

        group.MapGet("/getAll", async (ProductDbService db) =>
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

            var cursor = await db.GetAllProductsAsync();

            return Results.Stream(async responseStream =>
            {
                await foreach (var product in StreamProducts(cursor))
                    await responseStream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(product, options));
            });
        });

        group.MapGet("/getOne", async (ProductDbService db) =>
        {
            //todo 
        });

        group.MapPost("/modifyOne", async (ProductDbService db) =>
        {
            //todo 
        });
    }
}