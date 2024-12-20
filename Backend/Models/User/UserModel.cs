using Backend.Enums;
using Backend.Models.Identity;
using MongoDB.Bson.Serialization.Attributes;

namespace Backend.Models.User;

public class UserModel
{

    [BsonId]
    public long UserId { get; set; }
    public required EUserType EUserType { get; set; }
    public required bool EmailVerified { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string PasswordSalt { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required AddressModel[] AddressData { get; set; } = [];
    public UserMetrics UserMetrics { get; set; } = new();
}