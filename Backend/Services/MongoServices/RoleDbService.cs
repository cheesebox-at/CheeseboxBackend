using Backend.Enums;
using Backend.Models;
using Backend.Models.User;
using MongoDB.Driver;

namespace Backend.Services.MongoServices;

public class RoleDbService(
    IMongoCollection<RoleModel> roleCollection,
    IMongoCollection<DataStoreModel> dataStore,
    IMongoClient mongoClient,
    ILogger<RoleDbService> logger)
{
    public async Task<(bool IsSuccess, string Reason)> CreateRoleAsync(RoleModel role)
    {

        var result = (false, "Not defined");

        using var session = await mongoClient.StartSessionAsync();

        try
        {
            await session.WithTransactionAsync(async (handle, token) =>
            {
                var userIdFilter = Builders<DataStoreModel>.Filter.Eq(x => x.DataStoreType, EDataStore.HighestRoleId);
                var userIdUpdate = Builders<DataStoreModel>.Update.Inc(x => x.Value, 1);
                var roleId = await dataStore.FindOneAndUpdateAsync(handle, userIdFilter, userIdUpdate, new FindOneAndUpdateOptions<DataStoreModel>() { IsUpsert = true }, token);

                if (roleId is null)
                {
                    role.Id = 0;
                    role.Name = "SysAdmin";
                    role.Permissions = [Permissions.Main.IsAdmin];
                }
                else
                {
                    role.Id = roleId.Value;
                }

                await roleCollection.InsertOneAsync(handle, role, cancellationToken: token);
                logger.LogInformation("Created new role. RoleId: {roleId} RoleName: {roleName}.", role.Id, role.Name);
                return Task.CompletedTask;
            });
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to create user: {ex}", ex);
            return (false, "Failed to create user.");
        }

        return (true, role.Id.ToString());
    }

    /// <summary>
    /// throws exception if none or multiple roles are found.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<RoleModel?> GetRoleByIdAsync(long id)
    {
        var filter = Builders<RoleModel>.Filter.Eq(x => x.Id, id);

        var result = await roleCollection.FindAsync(filter);

        return await result.SingleAsync();
    }

    /// <summary>
    /// Deletes all roles with the specified id.
    /// </summary>
    /// <param name="id"></param>
    /// <returns>True if one or more roles have been delted. False if no roles were deleted.</returns>
    public async Task<bool> DeleteRoleByIdAsync(long id)
    {
        var filter = Builders<RoleModel>.Filter.Eq(x => x.Id, id);
        var result = await roleCollection.DeleteManyAsync(filter);

        return result.DeletedCount != 0;
    }

    public async Task<RoleModel> UpdateRoleAsync(RoleModel role)
    {
        var filter = Builders<RoleModel>.Filter.Eq(x => x.Id, role.Id);

        var options = new FindOneAndReplaceOptions<RoleModel> { ReturnDocument = ReturnDocument.After };
        
        return await roleCollection.FindOneAndReplaceAsync(filter, role, options);
    }
}