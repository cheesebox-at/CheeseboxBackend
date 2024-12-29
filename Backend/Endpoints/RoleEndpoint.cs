using Backend.DTOs;
using Backend.Middleware.Authorization;
using Backend.Models.Permissions;
using Backend.Models.User;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Endpoints;

public class RoleEndpoint
{
    public void Register(RouteGroupBuilder app)
    {
        var group = app.MapGroup("/roles");

        group.MapPost("/create", [Authorize] async (
            string? roleName,
            HttpContext context,
            ILogger<RoleEndpoint> logger,
            RoleDbService roleDbService) =>
        {
            
            
            
            var role = new RoleModel
            {
                Name = roleName ?? "Not defined"
            };

            try
            {
                var result = await roleDbService.CreateOneAsync(role);
                if (result.IsSuccess)
                    return Results.Ok(result.Reason);

                return Results.BadRequest(result.Reason);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed creating role:{roleName}", roleName);
                return Results.InternalServerError();
            }
        }).WithMetadata(new RequiredPermissionAttribute(Permissions.Roles.Create));
        
        group.MapGet("/get", [Authorize] async (
            long roleId,
            HttpContext context,
            ILogger<RoleEndpoint> logger,
            RoleDbService roleDbService) =>
        {
            try
            {
                return Results.Ok(await roleDbService.GetOneByIdAsync(roleId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }).WithMetadata(new RequiredPermissionAttribute(Permissions.Roles.View));
        
        group.MapPost("/delete",[Authorize] async (
            long roleId,
            HttpContext context,
            ILogger<RoleEndpoint> logger,
            RoleDbService roleDbService) =>
        {
            if (roleId == 0)
            {
                logger.LogInformation("Attempted to delete RoleId: 0. Blocked because it is a required role.");
                return Results.BadRequest("Can't delete the SysAdmin Role. ID: 0");
            }
            try
            {
                await roleDbService.DeleteOneByIdAsync(roleId);
                
                return Results.Ok();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }).WithMetadata(new RequiredPermissionAttribute(Permissions.Roles.Delete));

        group.MapPost("/modify", [Authorize] async (
            [FromBody] RoleModel role,
            long roleId,
            HttpContext context,
            ILogger<RoleEndpoint> logger,
            RoleDbService roleDbService) =>
        {
            if(roleId == 0)
                return Results.BadRequest("Only SysAdmins can modify the SysAdmin Role. ID: 0");
            
            if(roleId != role.Id)
                return Results.BadRequest("roleId and role do not match.");
            
            var result = await roleDbService.UpdateRoleAsync(role);

            if (result.Equals(role))
                return Results.BadRequest("Failed to update role. Role is still the same as before the update.");
            
            return Results.Ok(result);
        }).WithMetadata(new RequiredPermissionAttribute(Permissions.Roles.Edit));
    }
}