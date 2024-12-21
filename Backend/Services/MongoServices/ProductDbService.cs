using Backend.Models.Product;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Backend.Services.MongoServices;

public class ProductDbService(IMongoCollection<ProductModel> productCollection, ILogger<ProductDbService> logger)
{
    public async Task<ProductModel?> AddProductAsync(ProductModel product)
    {
        await productCollection.InsertOneAsync(product);

        if (product.Id == ObjectId.Empty)
            return null;

        return product;
    }

    public async Task<IAsyncCursor<ProductModel>> GetAllProductsAsync()
    {
        var result = await productCollection.FindAsync(FilterDefinition<ProductModel>.Empty);

        return result;
    }
    
    /// <summary>
    /// throws exception if none or multiple roles are found.
    /// </summary>
    /// <param name="id"></param>
    /// <returns>A single ProductModel</returns>
    public async Task<ProductModel> GetOneByIdAsync(ObjectId id)
    {
        var filter = Builders<ProductModel>.Filter.Eq(x => x.Id, id);

        var result = await productCollection.FindAsync(filter);

        return await result.SingleAsync();
    }
    
    public async Task<ProductModel> UpdateOneAsync(ProductModel product)
    {
        var filter = Builders<ProductModel>.Filter.Eq(x => x.Id, product.Id);

        var options = new FindOneAndReplaceOptions<ProductModel> { ReturnDocument = ReturnDocument.After };
        
        return await productCollection.FindOneAndReplaceAsync(filter, product, options);
    }
}