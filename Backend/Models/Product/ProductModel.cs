using System.Text.Json.Serialization;
using Backend.Enums;
using Backend.JsonConverter;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Backend.Models.Product;

public class ProductModel
{
    [BsonId]
    [JsonConverter(typeof(ObjectIdStringConverter))]
    public ObjectId Id { get; set; }

    public required EProductTypes Type { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public required string ImageName { get; set; }
    public string[] Features { get; set; } = [];
    public float BasePrice { get; set; }
    public float PricePerHour { get; set; }
    public int InStock { get; set; }
    public int MaxQuantity { get; set; }
    public int MinQuantity { get; set; }
}