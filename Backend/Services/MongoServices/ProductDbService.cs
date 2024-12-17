using Backend.Models.Product;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Backend.Services.MongoServices;

public class ProductDbService(IMongoCollection<ProductModel> productDb, ILogger<ProductDbService> logger)
{
    public async Task<ProductModel?> AddProductAsync(ProductModel product)
    {
        await productDb.InsertOneAsync(product);

        if (product.Id == ObjectId.Empty)
            return null;

        return product;
    }

    public async Task<IAsyncCursor<ProductModel>> GetAllProductsAsync()
    {
        var result = await productDb.FindAsync(FilterDefinition<ProductModel>.Empty);

        return result;
    }
}