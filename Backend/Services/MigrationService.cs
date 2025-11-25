using System.Text.Json;
using Backend.Enums;
using Backend.Models.Order;
using Backend.Models.Product;
using Backend.Services.MongoServices;

namespace Backend.Services;

public class MigrationService(
    ProductDbService productDb,
    OrderDbService orderDb,
    ILogger<MigrationService> logger,
    IWebHostEnvironment env)
{
    private readonly string _jsonPath = Path.Combine(env.ContentRootPath, "..", "CheeseBox-Website", "public", "data", "database.json");
    
    public async Task<bool> IsMigrationNeededAsync()
    {
        // Check if products exist in database
        var cursor = await productDb.GetAllProductsAsync();
        var products = new List<ProductModel>();
        while (await cursor.MoveNextAsync())
        {
            products.AddRange(cursor.Current);
        }
        
        return products.Count == 0;
    }
    
    public async Task MigrateDataAsync()
    {
        logger.LogInformation("Starting data migration from database.json");
        
        if (!File.Exists(_jsonPath))
        {
            logger.LogWarning("database.json not found at {Path}", _jsonPath);
            return;
        }
        
        try
        {
            var jsonContent = await File.ReadAllTextAsync(_jsonPath);
            var data = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            
            // Migrate products
            await MigrateProductsAsync(data);
            
            logger.LogInformation("Data migration completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during data migration");
            throw;
        }
    }
    
    private async Task MigrateProductsAsync(JsonElement data)
    {
        if (!data.TryGetProperty("products", out var productsElement))
        {
            logger.LogWarning("No products found in database.json");
            return;
        }
        
        // Migrate main products
        if (productsElement.TryGetProperty("main", out var mainProducts))
        {
            foreach (var productElement in mainProducts.EnumerateArray())
            {
                try
                {
                    var product = new ProductModel
                    {
                        Type = EProductTypes.Rentable,
                        Name = productElement.GetProperty("name").GetString() ?? "",
                        Description = productElement.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        ImageName = ExtractImageName(productElement.GetProperty("image").GetString() ?? ""),
                        Features = productElement.TryGetProperty("features", out var features)
                            ? features.EnumerateArray().Select(f => f.GetString() ?? "").ToArray()
                            : Array.Empty<string>(),
                        BasePrice = productElement.GetProperty("basePrice").GetSingle(),
                        PricePerHour = productElement.TryGetProperty("pricePerHour", out var pph) ? pph.GetSingle() : 0,
                        InStock = 1,
                        MaxQuantity = 1,
                        MinQuantity = 1,
                        IsPublished = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    await productDb.AddProductAsync(product);
                    logger.LogInformation("Migrated main product: {Name}", product.Name);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error migrating main product");
                }
            }
        }
        
        // Migrate additional products
        if (productsElement.TryGetProperty("additional", out var additionalProducts))
        {
            foreach (var productElement in additionalProducts.EnumerateArray())
            {
                try
                {
                    var product = new ProductModel
                    {
                        Type = EProductTypes.PurchasableAddon,
                        Name = productElement.GetProperty("name").GetString() ?? "",
                        Description = productElement.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        ImageName = ExtractImageName(productElement.GetProperty("image").GetString() ?? ""),
                        Features = Array.Empty<string>(),
                        BasePrice = productElement.GetProperty("price").GetSingle(),
                        PricePerHour = 0,
                        InStock = 10,
                        MaxQuantity = productElement.TryGetProperty("maxQuantity", out var maxQty) ? maxQty.GetInt32() : 5,
                        MinQuantity = 1,
                        IsPublished = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    
                    await productDb.AddProductAsync(product);
                    logger.LogInformation("Migrated additional product: {Name}", product.Name);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error migrating additional product");
                }
            }
        }
    }
    
    private string ExtractImageName(string imagePath)
    {
        // Extract filename from path like "/assets/products/CheeseBoxClassic.webp"
        return Path.GetFileName(imagePath);
    }
}



