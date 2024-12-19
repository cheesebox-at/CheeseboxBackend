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
    ILogger<UserDbService> logger)
{
    public async Task<(bool IsSuccess, string Reason)> CreateNewUser(UserModel user)
    {
        var result = (false, "Not defined");
        
        using var session = await mongoClient.StartSessionAsync();

        try
        {
            await session.WithTransactionAsync(async (handle, token) =>
            {
                var userIdFilter = Builders<DataStoreModel>.Filter.Eq(x => x.DataStoreType, EDataStore.HighestUserId);
                var userIdUpdate = Builders<DataStoreModel>.Update.Inc(x => x.Value, 1);
                var userId = await dataStore.FindOneAndUpdateAsync(handle, userIdFilter, userIdUpdate, new FindOneAndUpdateOptions<DataStoreModel>(){IsUpsert = true}, token);

                if (userId is null)
                {
                    user.UserId = 0;
                    user.EUserType = EUserType.SysAdmin;
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
        catch(InvalidOperationException ex)
        {
            logger.LogInformation("Tried to create user with already existing email: {email}", user.Email);
            return (false, ex.Message);
            await session.AbortTransactionAsync();
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to create user: {ex}", ex);
            return (false, "Failed to create user.");
            await session.AbortTransactionAsync();
        }
        return (true, user.UserId.ToString());
    }
    
    public async Task Login(string username, string password)
    {
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