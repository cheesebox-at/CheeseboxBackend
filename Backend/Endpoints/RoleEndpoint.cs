using Backend.DTOs;
using Backend.Models.User;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Endpoints;

public class RoleEndpoint
{
    public void Register(RouteGroupBuilder app)
    {
        var group = app.MapGroup("/roles");

        group.MapPost("/create", async (
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
                var result = await roleDbService.CreateRoleAsync(role);
                if (result.IsSuccess)
                    return Results.Ok(result.Reason);

                return Results.BadRequest(result.Reason);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed creating role:{roleName}", roleName);
                return Results.InternalServerError();
            }
        });
        group.MapGet("/get", async (
            long roleId,
            HttpContext context,
            ILogger<RoleEndpoint> logger,
            RoleDbService roleDbService) =>
        {
            try
            {
                return Results.Ok(await roleDbService.GetRoleByIdAsync(roleId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });
        group.MapPost("/delete", async (
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
                return Results.Ok(await roleDbService.DeleteRoleByIdAsync(roleId));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });
        group.MapPost("/modify", async (
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
        });
    }
}