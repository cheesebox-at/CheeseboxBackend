using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.DTOs;
using Backend.Models.Configuration;
using Backend.Models.User;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;

namespace Backend.Services;

public class SessionService(
    IMongoCollection<SessionModel> sessionCollection, 
    IOptions<SessionConfigurationModel> sessionConfiguration, 
    UserDbService userService,
    UserDbService userDbService)
{
    /// <summary>
    /// Throws an exception if there are more than one user found in the database.
    /// </summary>
    /// <param name="loginDto"></param>
    /// <returns></returns>
    public async Task<bool> VerifyPasswordAsync(LoginDto loginDto)
    {
        try
        {
            var user = await userDbService.GetUserAsync(loginDto.Email);
            
            var dbPassHash = user.PasswordHash;
            var passHash = HashPassword(loginDto.Password, Convert.FromBase64String(user.PasswordSalt));
        
            if (passHash != dbPassHash)
                return false;
        }
        catch (ArgumentNullException ae)
        {
            // logger.LogCritical("Invalid password salt length. Salt needs to be 16 bytes.");
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
        
        return true;
    }
    
    public string HashPassword(string password, byte[] salt)
    {
        if(salt.Length != 16)
            throw new ArgumentException("Salt must be 16 bytes");
        
        var passwordHash = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA512, 100_000, 32);

        return Convert.ToBase64String(passwordHash);
    }
    
    public async Task<SessionModel> CreateSessionAsync(UserModel user)
    {
        var refreshToken = GenerateRefreshToken();
        
        var session = new SessionModel
        {
            UserId = user.UserId,
            RefreshToken = refreshToken,
            ExpireAt = DateTime.UtcNow.AddDays(sessionConfiguration.Value.ExpireAfterDays)
        };
        
        await sessionCollection.InsertOneAsync(session);
        return session;
    }    
    
    /// <summary>
    /// throws exception if refreshtoken can't be updated.
    /// </summary>
    /// <param name="sessionId"></param>
    /// <returns></returns>
    public async Task<string> UpdateRefreshTokenAsync(string refreshToken)
    {
        //todo add datetime to sessionmodel so that it can only be updated and checked every set amount of time.
        //todo add useragent to ensure the session is only used on the client it was started.
        var newRefreshToken = GenerateRefreshToken();
        
        var filter = Builders<SessionModel>.Filter.Eq(s => s.RefreshToken, refreshToken);
        var update = Builders<SessionModel>.Update.Set(x => x.RefreshToken, newRefreshToken);
        
        var result = await sessionCollection.UpdateOneAsync(filter, update);

        if (result.ModifiedCount == 1)
            return newRefreshToken;

        throw new Exception("Failed to update refresh token");
    }    
    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        RandomNumberGenerator.Fill(randomNumber);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        
        return $"{Convert.ToBase64String(randomNumber)}.{timestamp}";
    }

    /// <summary>
    /// Will throw an exception if more than one session is found
    /// </summary>
    /// <param name="refreshToken"></param>
    /// <returns></returns>
    public async Task<bool> ValidateRefreshTokenAsync(string refreshToken)
    {
        var filter = Builders<SessionModel>.Filter.Eq(s => s.RefreshToken, refreshToken);
        var result = await (await sessionCollection.FindAsync(filter)).SingleAsync();
        
        return result.ExpireAt > DateTime.UtcNow;
    }
    public async Task<string?> GenerateJwtTokenAsync(string refreshToken)
    {
        
        var session = await GetSessionAsync(refreshToken);
        
        var user = await userService.GetUserAsync(session.UserId);
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(sessionConfiguration.Value.JwtKey);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, ObjectId.GenerateNewId().ToString()),
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new("userId", user.UserId.ToString()),
            new("sessionId", session.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Email)
        };

        foreach (var role in user.RolesIds)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        }

        var token = new JwtSecurityToken
        (
            issuer: sessionConfiguration.Value.JwtIssuer,
            audience: sessionConfiguration.Value.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(sessionConfiguration.Value.JwtExpireAfterMinutes),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        );
        
        var jwtToken = tokenHandler.WriteToken(token);
        
        return jwtToken;
    }

    public ClaimsPrincipal GetClaimsPrincipalFromJwtAsync(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        
        var tokenValidation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = sessionConfiguration.Value.JwtIssuer,
            ValidAudience = sessionConfiguration.Value.JwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(sessionConfiguration.Value.JwtKey))
        };
        
        return tokenHandler.ValidateToken(token, tokenValidation, out _);
    }
    
    /// <summary>
    /// Throws an exception if more than 1 session is found.
    /// </summary>
    /// <param name="refreshToken"></param>
    /// <returns></returns>
    private async Task<SessionModel> GetSessionAsync(string refreshToken)
    {
        var filter = Builders<SessionModel>.Filter.Eq(s => s.RefreshToken, refreshToken);

        return await (await sessionCollection.FindAsync(filter)).SingleAsync();
    }
    public async Task TerminateSessionAsync(Guid sessionId)
    {
    }
    public async Task TerminateAllSessionsAsync(long userId)
    {
        await sessionCollection.DeleteManyAsync(x => x.UserId == userId);
    }
    public async Task TerminateAllSessionsAsync(bool includeAdmins = true)
    {
        if (includeAdmins)
        {
            await sessionCollection.Database.DropCollectionAsync(sessionCollection.CollectionNamespace.CollectionName);
            return;
        }
     
        //todo include filter to exclude admin user sessions
        // var filter = Builders<SessionModel>.Filter.Where(x => );
    }
}