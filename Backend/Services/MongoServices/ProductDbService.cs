using Backend.Models.Product;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Backend.Services.MongoServices;

public class ProductDbService(IMongoCollection<ProductModel> productDb, ILogger<ProductDbService> logger)
{
    public async Task<ProductModel?> AddProductAsync(ProductModel product)
    {
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        await productDb.InsertOneAsync(product);
        
        // MongoDB will automatically assign an ObjectId if none is set,
        // so we can safely return the product after insert.
        return product;
    }

    public async Task<IAsyncCursor<ProductModel>> GetAllProductsAsync()
    {
        var result = await productDb.FindAsync(FilterDefinition<ProductModel>.Empty);
        return result;
    }
    
    public async Task<List<ProductModel>> GetPublishedProductsAsync()
    {
        var filter = Builders<ProductModel>.Filter.Eq(p => p.IsPublished, true);
        var result = await productDb.Find(filter).ToListAsync();
        return result;
    }
    
    public async Task<ProductModel?> GetProductByIdAsync(ObjectId id)
    {
        var filter = Builders<ProductModel>.Filter.Eq(p => p.Id, id);
        var result = await productDb.Find(filter).FirstOrDefaultAsync();
        return result;
    }
    
    public async Task<ProductModel?> GetProductByIdAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var objectId))
            return null;
            
        return await GetProductByIdAsync(objectId);
    }
    
    public async Task<bool> UpdateProductAsync(ObjectId id, ProductModel product)
    {
        product.UpdatedAt = DateTime.UtcNow;
        var filter = Builders<ProductModel>.Filter.Eq(p => p.Id, id);
        var result = await productDb.ReplaceOneAsync(filter, product);
        return result.ModifiedCount > 0;
    }
    
    public async Task<bool> DeleteProductAsync(ObjectId id)
    {
        var filter = Builders<ProductModel>.Filter.Eq(p => p.Id, id);
        var result = await productDb.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }
    
    public async Task<bool> PublishProductAsync(ObjectId id)
    {
        var update = Builders<ProductModel>.Update
            .Set(p => p.IsPublished, true)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        var filter = Builders<ProductModel>.Filter.Eq(p => p.Id, id);
        var result = await productDb.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }
    
    public async Task<bool> UnpublishProductAsync(ObjectId id)
    {
        var update = Builders<ProductModel>.Update
            .Set(p => p.IsPublished, false)
            .Set(p => p.UpdatedAt, DateTime.UtcNow);
        var filter = Builders<ProductModel>.Filter.Eq(p => p.Id, id);
        var result = await productDb.UpdateOneAsync(filter, update);
        return result.ModifiedCount > 0;
    }
}