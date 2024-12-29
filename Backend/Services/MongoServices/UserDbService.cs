using System.Globalization;
using System.Text;
using Backend.Models.User;
using Backend.DTOs;
using Backend.Enums;
using Backend.Models;
using Backend.Models.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.JSInterop.Infrastructure;
using MongoDB.Driver;

namespace Backend.Services.MongoServices;

public class UserDbService(
    IMongoClient mongoClient, 
    IMongoCollection<UserModel> userCollection,
    IMongoCollection<DataStoreModel> dataStore,
    RoleDbService roleDbService,
    ILogger<UserDbService> logger)
{
    public async Task<(bool IsSuccess, string Reason)> CreateOneAsync(UserModel user)
    {
        var result = (false, "Not defined");
        
        using var session = await mongoClient.StartSessionAsync();

        try
        {
            await session.WithTransactionAsync(async (handle, token) =>
            {
                var userIdFilter = Builders<DataStoreModel>.Filter.Eq(x => x.DataStoreType, EDataStore.HighestUserId);
                var userIdUpdate = Builders<DataStoreModel>.Update.Inc(x => x.Value, 1);
                var userId = await dataStore.FindOneAndUpdateAsync(handle, userIdFilter, userIdUpdate,
                    new FindOneAndUpdateOptions<DataStoreModel>() { IsUpsert = true }, token);

                if (userId is null)
                {
                    user.UserId = 0;
                    user.EUserType = EUserType.SysAdmin;
                    user.RolesIds = [0];

                    try
                    {
                        var sysAdminRole = roleDbService.GetOneByIdAsync(0);
                    }
                    catch (InvalidOperationException ex)
                    {
                        try
                        {
                            await roleDbService.CreateOneAsync(new RoleModel());
                        }
                        catch (Exception e)
                        {
                            logger.LogCritical(e,
                                "Failed automatically creating SysAdmin role while creating SysAdmin User.");
                            throw new ApplicationException("Error creating role", e);
                        }
                    }
                }
                else
                {
                    user.UserId = userId.Value;
                }

                var filter = Builders<UserModel>.Filter.Eq(x => x.Email, user.Email);

                var existingCount = await userCollection.CountDocumentsAsync(handle, filter, cancellationToken: token);
                if (existingCount != 0)
                    throw new InvalidOperationException($"A user with the email '{user.Email}' already exists.");

                await userCollection.InsertOneAsync(handle, user, cancellationToken: token);
                logger.LogInformation("Created new user. UserId: {userId} email {email}.", user.UserId, user.Email);
                return Task.CompletedTask;
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogInformation("Tried to create user with already existing email: {email}", user.Email);
            return (false, ex.Message);
            await session.AbortTransactionAsync();
        }
        catch (ApplicationException ex)
        {
            logger.LogCritical(ex, "Aborted SysAdmin account creation. Failed to create SysAdmin. The first user and role created are always SysAdmin.");
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to create user: {ex}", ex);
            return (false, "Failed to create user.");
            await session.AbortTransactionAsync();
        }
        return (true, user.UserId.ToString());
    }
    
    /// <summary>
    /// Assigns roles to user by id. If the user already has the role it does not add duplicates.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="roleIds"></param>
    /// <returns>Returns true if the user was found and mongodb executed the call. Even if the user already had the role.</returns>
    public async Task<bool> AssignRoles(long userId, long[] roleIds)
    {
        var filter = Builders<UserModel>.Filter.Eq(user => user.UserId, userId);
        var update = Builders<UserModel>.Update.AddToSetEach(x => x.RolesIds, roleIds);
        
        var result = await userCollection.UpdateManyAsync(filter, update);

        return result.MatchedCount > 0;
    }
    /// <summary>
    /// Removes roles from the user by id.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="roleIds"></param>
    /// <returns>Returns true if the user was found and mongodb executed the call. Even if the user didn't have the role.</returns>
    public async Task<bool> RemoveRoles(long userId, long[] roleIds)
    {
        var filter = Builders<UserModel>.Filter.Eq(user => user.UserId, userId);
        var update = Builders<UserModel>.Update.PullAll(x => x.RolesIds, roleIds);
        
        var result = await userCollection.UpdateManyAsync(filter, update);

        return result.MatchedCount > 0;
    }
    /// <summary>
    /// Removes a role from all users based on the roleId.
    /// </summary>
    /// <param name="roleIdToRemove"></param>
    /// <returns>Returns the amount of users the role was removed from.</returns>
    public async Task<long> RemoveRoleFromAllUsersAsync(long roleIdToRemove)
    {
        var filter = Builders<UserModel>.Filter.AnyEq(user => user.RolesIds, roleIdToRemove);
        var update = Builders<UserModel>.Update.PullFilter(user => user.RolesIds, roleId => roleId == roleIdToRemove);
        
        var result = await userCollection.UpdateManyAsync(filter, update);
        
        return result.ModifiedCount;
    }
    
    /// <summary>
    /// Throws Exception if more than one user is found in db.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<UserModel> GetUserAsync(long userId)
    {
        var filter = Builders<UserModel>.Filter.Eq(x => x.UserId, userId);
        var user = await (await userCollection.FindAsync(filter)).SingleAsync();

        return user;
    }
    /// <summary>
    /// Throws Exception if more than one user is found in db.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    public async Task<UserModel> GetUserAsync(string email)
    {
        var filter = Builders<UserModel>.Filter.Eq(x => x.Email, email);
        var user = await (await userCollection.FindAsync(filter)).SingleAsync();

        return user;
    }
    

}