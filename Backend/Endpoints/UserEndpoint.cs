using Backend.DTOs;
using Backend.Middleware.Authorization;
using Backend.Models.Permissions;
using Backend.Models.User;
using Backend.Services;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Endpoints;

public class UserEndpoint
{
    public void Register(RouteGroupBuilder app)
    {
        var group = app.MapGroup("/user");
        
        group.MapPost("/create", [Authorize] async (
            CreateUserDto userDto,
            HttpContext context,
            ILogger<RoleEndpoint> logger,
            UserService userService) =>
        {
            (bool IsSuccess, string Reason) result;
            
            try
            {
                result = await userService.Create(userDto);
            }
            catch
            {
                return Results.InternalServerError();
            }
            
            if(result.IsSuccess)
                return Results.Ok(result.Reason);
            
            return Results.BadRequest(result.Reason);
        }).WithMetadata(new RequiredPermissionAttribute(Permissions.Users.Create));
        
                        
        group.MapPost("/removeRoleFromAllUsers",[Authorize] async (
            long roleId,
            HttpContext context,
            ILogger<RoleEndpoint> logger,
            UserDbService userDbService) =>
        {
            if (roleId == 0)
            {
                logger.LogInformation("Attempted to call removeRoleFromAllUsers with RoleId: 0. Blocked because it is a required role.");
                return Results.BadRequest("Can't delete the SysAdmin Role. ID: 0");
            }
            try
            {
                var modifiedCount = await userDbService.RemoveRoleFromAllUsersAsync(roleId);
                
                return Results.Ok(modifiedCount);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }).WithMetadata(new RequiredPermissionAttribute(Permissions.Users.ManageRoles));
        
        group.MapPost("/assignRoles",[Authorize] async (
            [FromBody] long[] roleIds,
            long userId,
            HttpContext context,
            ILogger<RoleEndpoint> logger,
            UserDbService userDbService) =>
        {
            if (roleIds.Contains(0))
            {
                logger.LogInformation("Attempted to assign Role with RoleId: 0. Blocked because it is the main sysAdmin role.");
                return Results.BadRequest("Can't assign the SysAdmin Role. ID: 0");
            }
            try
            {
                if (await userDbService.AssignRoles(userId, roleIds))
                {
                    return Results.Ok();
                }
                
                return Results.BadRequest("User not found.");
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }).WithMetadata(new RequiredPermissionAttribute(Permissions.Users.ManageRoles));
        
        group.MapPost("/removeRoles",[Authorize] async (
            [FromBody] long[] roleIds,
            long userId,
            HttpContext context,
            ILogger<RoleEndpoint> logger,
            UserDbService userDbService) =>
        {
            if (roleIds.Contains(0))
            {
                logger.LogInformation("Attempted to remove Role with RoleId: 0. Blocked because it is the main sysAdmin role.");
                return Results.BadRequest("Can't remove the SysAdmin Role. ID: 0");
            }
            try
            {
                if (await userDbService.RemoveRoles(userId, roleIds))
                {
                    return Results.Ok();
                }
                
                return Results.BadRequest("User not found.");
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }).WithMetadata(new RequiredPermissionAttribute(Permissions.Users.ManageRoles));
    }
}