using Backend.Enums;
using Backend.Models.Identity;
using MongoDB.Bson.Serialization.Attributes;

namespace Backend.Models.User;

public class UserModel
{
    [BsonId]
    public long UserId { get; set; }
    public required EUserType EUserType { get; set; }
    public long[] RolesIds { get; set; } = [];
    public required bool EmailVerified { get; set; }
    public required string Email { get; set; }
    public string? PasswordHash { get; set; }  // Nullable for OAuth users
    public string? PasswordSalt { get; set; }  // Nullable for OAuth users
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required AddressModel[] AddressData { get; set; } = [];
    public string Phone { get; set; } = string.Empty;
    public UserMetrics UserMetrics { get; set; } = new();
    
    // OAuth provider information
    public string? GoogleId { get; set; }  // Google's unique user ID
    public string? ProfilePictureUrl { get; set; }  // Profile picture from Google
    public string? AuthProvider { get; set; }  // "local" or "google"
}