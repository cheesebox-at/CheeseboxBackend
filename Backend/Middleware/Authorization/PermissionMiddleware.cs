using System.Security.Claims;
using Backend.Models.User;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Middleware.Authorization;

public class PermissionMiddleware(RequestDelegate next, RoleDbService roleDbService, ILogger<PermissionMiddleware> logger)
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
        if (!context.User.Identity.IsAuthenticated) //todo check if this warning is valid or not.
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
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
        
        if (!roleIds.Any())
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
                role = await roleDbService.GetRoleByIdAsync(roleId);
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
}

