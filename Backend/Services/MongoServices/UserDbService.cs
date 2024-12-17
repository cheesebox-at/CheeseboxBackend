using System.Text;
using Backend.Models.User;
using Backend.DTOs;
using Backend.Enums;
using Backend.Models.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.JSInterop.Infrastructure;
using MongoDB.Driver;

namespace Backend.Services.MongoServices;

public class UserDbService(IMongoCollection<UserModel> userCollection)
{
    public async Task<(bool IsSuccess, string Reason)> CreateNewUser(UserModel user)
    {
        var filter = Builders<UserModel>.Filter.Eq(x => x.Email, user.Email);

        var existingCount = await userCollection.CountDocumentsAsync(filter);
        if (existingCount != 0)
            return (false, $"Email {user.Email} already exists.");
        
        await userCollection.InsertOneAsync(user);
        return (true, "User created");
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