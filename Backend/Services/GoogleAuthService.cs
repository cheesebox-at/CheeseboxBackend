using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Backend.Models.Configuration;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class GoogleAuthService
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;

    public GoogleAuthService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _clientId = configuration["Google:ClientId"] ?? 
            throw new InvalidOperationException("Google:ClientId not configured");
    }

    /// <summary>
    /// Validates a Google ID token and returns the user information
    /// </summary>
    public async Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken)
    {
        try
        {
            // Validate the token with Google's tokeninfo endpoint
            var response = await _httpClient.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}");

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var tokenInfo = await response.Content.ReadFromJsonAsync<GoogleTokenInfo>();

            if (tokenInfo == null)
            {
                return null;
            }

            // Verify the token was issued for our app
            if (tokenInfo.Aud != _clientId)
            {
                return null;
            }

            // Verify the token hasn't expired
            if (tokenInfo.Exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                return null;
            }

            return new GoogleUserInfo
            {
                GoogleId = tokenInfo.Sub,
                Email = tokenInfo.Email,
                EmailVerified = tokenInfo.EmailVerified == "true",
                FirstName = tokenInfo.GivenName ?? "",
                LastName = tokenInfo.FamilyName ?? "",
                ProfilePictureUrl = tokenInfo.Picture
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error validating Google token: {ex.Message}");
            return null;
        }
    }
}

public class GoogleTokenInfo
{
    [JsonPropertyName("iss")]
    public string? Iss { get; set; }

    [JsonPropertyName("azp")]
    public string? Azp { get; set; }

    [JsonPropertyName("aud")]
    public string? Aud { get; set; }

    [JsonPropertyName("sub")]
    public string? Sub { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("email_verified")]
    public string? EmailVerified { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("picture")]
    public string? Picture { get; set; }

    [JsonPropertyName("given_name")]
    public string? GivenName { get; set; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; set; }

    [JsonPropertyName("exp")]
    public long Exp { get; set; }
}

public class GoogleUserInfo
{
    public required string GoogleId { get; set; }
    public required string Email { get; set; }
    public required bool EmailVerified { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? ProfilePictureUrl { get; set; }
}

