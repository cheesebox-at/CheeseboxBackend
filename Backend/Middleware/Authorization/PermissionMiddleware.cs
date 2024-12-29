using System.Security.Claims;
using Backend.Models.Configuration;
using Backend.Models.User;
using Backend.Services;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Backend.Middleware.Authorization;

public class PermissionMiddleware(
    RequestDelegate next, 
    RoleDbService roleDbService, 
    ILogger<PermissionMiddleware> logger, 
    SessionService sessionService, 
    IOptions<SessionConfigurationModel> sessionConfiguration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var authorizeAttribute = endpoint?.Metadata.GetMetadata<AuthorizeAttribute>();

        //Skip if no [Authorize] attribute is set on the endpoint
        if (authorizeAttribute is null)
        {
            await next(context);
            return;
        }
        
        // Ensure the user is authenticated (handled by default JWT middleware)
        if (context.User.Identity is { IsAuthenticated: false})
        {
            //if use is not authenticaed check if a valid refresh token is present and create a new pair.
            var newJwt = await TryRefreshTokens(context);
            
            if (newJwt is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }
            
            //this sets the claimsprincipal user which is usually done and authenticated by the authentication middleware. We don't need to authenticate here becase we create the jwt here as well.
            context.User = sessionService.GetClaimsPrincipalFromJwtAsync(newJwt);
        }

        // This checks if there is no custom permission attribute set on the requested endpoint.
        var permissionAttribute = endpoint?.Metadata.GetMetadata<RequiredPermissionAttribute>();
        if (permissionAttribute == null)
        {
            await next(context);
            return;
        }

        var roleIds = context.User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        if (roleIds.Contains("0"))
        {
            // Proceed to the next middleware or endpoint. RoleId 0 is always the SysAdmin role.
            await next(context);
            return;
        }
        
        if (roleIds.Count == 0)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden: No roles assigned.");
            return;
        }

        foreach (var roleIdString in roleIds)
        {
            if (!long.TryParse(roleIdString, out long roleId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync($"Forbidden: Missing permission '{permissionAttribute.Permission}'.");
                return;
            }

            RoleModel role;
            
            try
            {
                role = await roleDbService.GetOneByIdAsync(roleId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception from GetRoleByIdAsync while authorizing. {exception}", ex);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync($"Forbidden: Exception while getting role from db.");
                return;
            }
            
            // Check if the required permission is in the list
            if (role.Permissions.Contains(permissionAttribute.Permission))
            {
                // Proceed to the next middleware or endpoint
                await next(context);
                return;
            }
        }
        
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync($"Forbidden: Missing permission '{permissionAttribute.Permission}'.");
        return;
    }

    private async Task<string?> TryRefreshTokens(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue("refresh", out var refreshToken))
            return null;

        var refreshTokenValid = await sessionService.ValidateRefreshTokenAsync(refreshToken);

        if (!refreshTokenValid)
        {
            context.Response.Cookies.Delete("refresh"); //todo this doesn't seem to delete the cookie, at least not in postman
            return null;
        }

        string newRefreshToken;
        
        var jwt = await sessionService.GenerateJwtTokenAsync(refreshToken);
        if (jwt is null)
            return null;
            
        try
        {
            newRefreshToken = await sessionService.UpdateRefreshTokenAsync(refreshToken);
        }
        catch
        {
            return null;
        }
            
        var refreshCookieOptions = new CookieOptions
        {
            Expires = DateTime.UtcNow + TimeSpan.FromDays(sessionConfiguration.Value.ExpireAfterDays),
            HttpOnly = true,
            Secure = true,
            Path = "/api",
        };
        context.Response.Cookies.Append("refresh", newRefreshToken, refreshCookieOptions);
        
        var jwtCookieOptions = new CookieOptions
        {
            Expires = DateTime.UtcNow + TimeSpan.FromMinutes(sessionConfiguration.Value.JwtExpireAfterMinutes),
            HttpOnly = true,
            Secure = true,
            IsEssential = true // todo this can maybe be removed
        };
        context.Response.Cookies.Append("auth", jwt, jwtCookieOptions);
        return jwt;
    }
}

