using Backend.Enums;
using Backend.Models.Order;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Backend.Services.MongoServices;

public class OrderDbService(IMongoCollection<OrderModel> orderDb, ILogger<OrderDbService> logger)
{
    public async Task<OrderModel?> CreateOrderAsync(OrderModel order)
    {
        // Generate order number
        order.OrderNumber = await GenerateOrderNumberAsync();
        order.CreatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        
        await orderDb.InsertOneAsync(order);

        if (order.Id == ObjectId.Empty)
            return null;

        return order;
    }

    public async Task<OrderModel?> GetOrderByIdAsync(ObjectId id)
    {
        var filter = Builders<OrderModel>.Filter.Eq(o => o.Id, id);
        var result = await orderDb.Find(filter).FirstOrDefaultAsync();
        return result;
    }
    
    public async Task<OrderModel?> GetOrderByIdAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return null;
            
        return await GetOrderByIdAsync(objectId);
    }
    
    public async Task<OrderModel?> GetOrderByOrderNumberAsync(string orderNumber)
    {
        var filter = Builders<OrderModel>.Filter.Eq(o => o.OrderNumber, orderNumber);
        var result = await orderDb.Find(filter).FirstOrDefaultAsync();
        return result;
    }

    public async Task<List<OrderModel>> GetAllOrdersAsync()
    {
        var result = await orderDb.Find(FilterDefinition<OrderModel>.Empty)
            .SortByDescending(o => o.CreatedAt)
            .ToListAsync();
        return result;
    }

    public async Task<List<OrderModel>> GetOrdersByUserIdAsync(long userId)
    {
        var filter = Builders<OrderModel>.Filter.Eq(o => o.UserId, userId);
        var result = await orderDb.Find(filter)
            .SortByDescending(o => o.CreatedAt)
            .ToListAsync();
        return result;
    }

    public async Task<List<OrderModel>> GetOrdersByProductIdAsync(ObjectId productId)
    {
        var filter = Builders<OrderModel>.Filter.Eq(o => o.ProductId, productId);
        var result = await orderDb.Find(filter)
            .SortByDescending(o => o.CreatedAt)
            .ToListAsync();
        return result;
    }

    public async Task<List<OrderModel>> GetOrdersByDateRangeAsync(DateTime start, DateTime end)
    {
        var filter = Builders<OrderModel>.Filter.And(
            Builders<OrderModel>.Filter.Gte(o => o.StartDate, start),
            Builders<OrderModel>.Filter.Lte(o => o.EndDate, end)
        );
        
        var result = await orderDb.Find(filter)
            .SortByDescending(o => o.CreatedAt)
            .ToListAsync();
        return result;
    }

    /// <summary>
    /// Aggregates the total ordered quantity for each additional product across all
    /// non-cancelled orders that overlap with the given date range.
    /// </summary>
    public async Task<Dictionary<ObjectId, int>> GetAdditionalProductsUsageInDateRangeAsync(DateTime start, DateTime end)
    {
        // Filter for orders that overlap with the requested range AND are not cancelled
        var filter = Builders<OrderModel>.Filter.And(
            Builders<OrderModel>.Filter.Ne(o => o.Status, EOrderStatus.Cancelled),
            Builders<OrderModel>.Filter.Lt(o => o.StartDate, end),
            Builders<OrderModel>.Filter.Gt(o => o.EndDate, start)
        );

        var orders = await orderDb.Find(filter).ToListAsync();

        var usage = new Dictionary<ObjectId, int>();

        foreach (var order in orders)
        {
            foreach (var additional in order.AdditionalProducts)
            {
                if (!usage.TryGetValue(additional.ProductId, out var current))
                {
                    usage[additional.ProductId] = additional.Quantity;
                }
                else
                {
                    usage[additional.ProductId] = current + additional.Quantity;
                }
            }
        }

        return usage;
    }

    /// <summary>
    /// Aggregates the total ordered quantity for each additional product across all
    /// non-cancelled orders. (Legacy/Global usage)
    /// </summary>
    public async Task<Dictionary<ObjectId, int>> GetAdditionalProductsUsageAsync()
    {
        var filter = Builders<OrderModel>.Filter.Ne(o => o.Status, EOrderStatus.Cancelled);
        var orders = await orderDb.Find(filter).ToListAsync();

        var usage = new Dictionary<ObjectId, int>();

        foreach (var order in orders)
        {
            foreach (var additional in order.AdditionalProducts)
            {
                if (!usage.TryGetValue(additional.ProductId, out var current))
                {
                    usage[additional.ProductId] = additional.Quantity;
                }
                else
                {
                    usage[additional.ProductId] = current + additional.Quantity;
                }
            }
        }

        return usage;
    }

    public async Task<bool> UpdateOrderStatusAsync(ObjectId id, EOrderStatus status)
    {
        var update = Builders<OrderModel>.Update
            .Set(o => o.Status, status)
            .Set(o => o.UpdatedAt, DateTime.UtcNow);
        var filter = Builders<OrderModel>.Filter.Eq(o => o.Id, id);
        var result = await orderDb.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> CancelOrderAsync(ObjectId id)
    {
        return await UpdateOrderStatusAsync(id, EOrderStatus.Cancelled);
    }
    
    public async Task<List<(DateTime StartDate, DateTime EndDate)>> GetBookedTimeFramesAsync(ObjectId productId)
    {
        var filter = Builders<OrderModel>.Filter.And(
            Builders<OrderModel>.Filter.Eq(o => o.ProductId, productId),
            Builders<OrderModel>.Filter.Ne(o => o.Status, EOrderStatus.Cancelled)
        );
        
        var orders = await orderDb.Find(filter).ToListAsync();
        
        return orders.Select(o => (o.StartDate, o.EndDate)).ToList();
    }
    
    public async Task<bool> CheckProductAvailabilityAsync(ObjectId productId, DateTime start, DateTime end)
    {
        var bookedTimeFrames = await GetBookedTimeFramesAsync(productId);
        
        foreach (var (bookedStart, bookedEnd) in bookedTimeFrames)
        {
            // Check for overlap
            if (!(end <= bookedStart || start >= bookedEnd))
            {
                // There is an overlap
                return false;
            }
        }
        
        return true;
    }

    private async Task<string> GenerateOrderNumberAsync()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var count = await orderDb.CountDocumentsAsync(FilterDefinition<OrderModel>.Empty);
        return $"CB-{timestamp}-{count + 1:D4}";
    }

    public async Task<bool> DeleteOrderAsync(ObjectId id)
    {
        var filter = Builders<OrderModel>.Filter.Eq(o => o.Id, id);
        var result = await orderDb.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<long> DeleteOrdersAsync(List<ObjectId> ids)
    {
        var filter = Builders<OrderModel>.Filter.In(o => o.Id, ids);
        var result = await orderDb.DeleteManyAsync(filter);
        return result.DeletedCount;
    }
}
