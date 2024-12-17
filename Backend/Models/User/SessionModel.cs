using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Backend.Models.User;

public class SessionModel
{
    [BsonId]
    public ObjectId Id { get; set; }
    public required long UserId { get; set; }
    public required string RefreshToken { get; set; }
    public DateTime? ExpireAt { get; set; }
}