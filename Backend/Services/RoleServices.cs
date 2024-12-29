using Backend.Models.User;
using Backend.Services.MongoServices;

namespace Backend.Services;

public class RoleServices(RoleDbService roleDbService, ILogger<RoleServices> logger)
{
    public async Task<(bool IsSuccess, string Reason)> CreateRole(RoleModel role)
    {
        var status = "Success";
        
        if(ConfirmRoleNamePolicy(role.Name, out status))
            return (false, status);
        
        var result = await roleDbService.CreateOneAsync(role);
        
        return (result.IsSuccess, result.Reason);
    }

    private static bool ConfirmRoleNamePolicy(string roleName, out string statusMessage)
    {
        statusMessage = "Role name valid";

        if (roleName.Length < 20) 
            return true;
        
        statusMessage = "Role name is too long: " + roleName;
        return false;

    }
}