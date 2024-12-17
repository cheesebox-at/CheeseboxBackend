namespace Backend.Models.Configuration;

public class SessionConfigurationModel
{
    public required int ExpireAfterDays { get; set; } = 1;
    public required string JwtKey { get; set; } = string.Empty;
    public required string JwtIssuer { get; set; } = string.Empty;
    public required string JwtAudience { get; set; } = string.Empty;
    public required int JwtExpireAfterMinutes { get; set; } = 15;
}