using System.Text.Json.Serialization;
using Backend.Enums;
using Backend.JsonConverter;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Backend.Models.Order;

public class OrderModel
{
    [BsonId]
    [JsonConverter(typeof(ObjectIdStringConverter))]
    public ObjectId Id { get; set; }
    
    public string OrderNumber { get; set; } = string.Empty;
    public required long UserId { get; set; }
    
    [JsonConverter(typeof(ObjectIdStringConverter))]
    public required ObjectId ProductId { get; set; }
    
    public OrderedAdditionalProduct[] AdditionalProducts { get; set; } = [];
    
    public required DateTime StartDate { get; set; }
    public required DateTime EndDate { get; set; }
    public required int DurationHours { get; set; }
    
    public required EDeliveryOption DeliveryOption { get; set; }
    public DeliveryAddressModel? DeliveryAddress { get; set; }
    
    public required EOrderStatus Status { get; set; }
    
    public required decimal TotalPrice { get; set; }
    public required decimal OriginalPrice { get; set; }
    
    public string? AppliedDiscountId { get; set; }
    public string? PromoCodeId { get; set; }
    
    public required string PaymentMethod { get; set; }
    
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string UserPhone { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderedAdditionalProduct
{
    [JsonConverter(typeof(ObjectIdStringConverter))]
    public required ObjectId ProductId { get; set; }
    public required int Quantity { get; set; }
    public required decimal PricePerUnit { get; set; }
}

