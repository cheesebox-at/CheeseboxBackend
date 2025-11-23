using Backend.Enums;
using Backend.Models.Order;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Authorization;
using MongoDB.Bson;

namespace Backend.Endpoints;

public class OrderEndpoint
{
    public void Register(RouteGroupBuilder app)
    {
        var group = app.MapGroup("/order");
        
        group.MapPost("/create", async (OrderModel order, OrderDbService orderDb, ProductDbService productDb) =>
        {
            // Validate product exists and is published
            var product = await productDb.GetProductByIdAsync(order.ProductId);
            if (product == null)
                return Results.BadRequest("Product not found");
            
            if (!product.IsPublished)
                return Results.BadRequest("Product is not published");
            
            // Check availability
            var isAvailable = await orderDb.CheckProductAvailabilityAsync(order.ProductId, order.StartDate, order.EndDate);
            if (!isAvailable)
                return Results.BadRequest("Product is not available for the selected time frame");
            
            // Check stock
            if (product.InStock <= 0)
                return Results.BadRequest("Product is out of stock");
            
            var result = await orderDb.CreateOrderAsync(order);
            return result is not null ? Results.Ok(result) : Results.BadRequest();
        });

        group.MapGet("/getAll", async (OrderDbService db) =>
        {
            var orders = await db.GetAllOrdersAsync();
            return Results.Ok(orders);
        }).RequireAuthorization();
        
        group.MapGet("/getOne", async (string id, OrderDbService db) =>
        {
            var order = await db.GetOrderByIdAsync(id);
            return order is not null ? Results.Ok(order) : Results.NotFound();
        }).RequireAuthorization();
        
        group.MapGet("/getByOrderNumber", async (string orderNumber, OrderDbService db) =>
        {
            var order = await db.GetOrderByOrderNumberAsync(orderNumber);
            return order is not null ? Results.Ok(order) : Results.NotFound();
        }).RequireAuthorization();
        
        group.MapGet("/getByUser", async (long userId, OrderDbService db) =>
        {
            var orders = await db.GetOrdersByUserIdAsync(userId);
            return Results.Ok(orders);
        }).RequireAuthorization();
        
        group.MapPut("/updateStatus", async (string id, EOrderStatus status, OrderDbService db) =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return Results.BadRequest("Invalid order ID");
                
            var success = await db.UpdateOrderStatusAsync(objectId, status);
            return success ? Results.Ok() : Results.NotFound();
        }).RequireAuthorization();
        
        group.MapPost("/cancel", async (string id, OrderDbService db) =>
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return Results.BadRequest("Invalid order ID");
                
            var success = await db.CancelOrderAsync(objectId);
            return success ? Results.Ok() : Results.NotFound();
        }).RequireAuthorization();
        
        group.MapGet("/getStatistics", async (OrderDbService db) =>
        {
            var orders = await db.GetAllOrdersAsync();
            
            var statistics = new
            {
                pending = orders.Count(o => o.Status == EOrderStatus.Pending),
                confirmed = orders.Count(o => o.Status == EOrderStatus.Confirmed),
                completed = orders.Count(o => o.Status == EOrderStatus.Completed),
                cancelled = orders.Count(o => o.Status == EOrderStatus.Cancelled),
                total = orders.Count,
                totalRevenue = orders.Where(o => o.Status != EOrderStatus.Cancelled).Sum(o => o.TotalPrice),
                averageOrderValue = orders.Any() ? orders.Where(o => o.Status != EOrderStatus.Cancelled).Average(o => o.TotalPrice) : 0
            };
            
            return Results.Ok(statistics);
        }).RequireAuthorization();
    }
}

