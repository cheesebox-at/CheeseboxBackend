using System.ComponentModel.DataAnnotations;
using Backend.DTOs;
using Backend.Enums;
using Backend.Models;
using Backend.Models.User;
using Backend.Services.MongoServices;
using Backend.Models.Identity;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Backend.Services;

public class UserService(UserDbService userDbService, IMongoCollection<DataStoreModel> dataStore)
{

    public async Task<(bool IsSuccess, string Reason)> Register(RegisterDto dto)
    {
        var status = "Success";

        if (!ConfirmPasswordValidity(dto.Password, dto.ConfirmPassword, out status)) 
            return (false, status);

        if(!ConfirmEmailValidity(dto.Email.Trim(), out status))
            return (false, status);


        var filter = Builders<DataStoreModel>.Filter.Eq(x => x.DataStoreType, EDataStore.HighestUserId);
        var update = Builders<DataStoreModel>.Update.Inc(x => x.Value, 1);
        var userId = await dataStore.FindOneAndUpdateAsync(filter, update, new FindOneAndUpdateOptions<DataStoreModel>(){IsUpsert = true}); // todo this currently fails on the first register because the userId is null. This should not be the case anymore once the first user is created which should be automatically created as an admin user when it sees there are no users in the db.

        // if (userId == null)
        // {
        //     userId = new DataStoreModel { DataStoreType = EDataStore.HighestUserId, Value = 1000 };
        //     await dataStore.InsertOneAsync(userId); // todo maybe make some validation here if it worked or not
        // }
        
        var result = await userDbService.CreateNewUser(new UserModel
        {
            UserId = userId.Value,
            EUserType = EUserType.User,
            EmailVerified = false,
            Email = dto.Email.Trim(),
            PasswordHash = dto.Password, //todo hash password
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            AddressData = (dto.AddressData ?? [])! // Suppress warning because the code actually works, the warning is wrong.
        });

        if(!result.IsSuccess)
            return (false, result.Reason);
        
        return (true, "Success");
    }
    
    private bool ConfirmPasswordValidity(string password, string? confirmPassword, out string statusMessage)
    {
        statusMessage = "Password valid";

        if (confirmPassword is not null && password != confirmPassword)
        {
            statusMessage = "Passwords do not match";
            return false;
        }

        return true;
    }

    private bool ConfirmEmailValidity(string email, out string statusMessage)
    {
        statusMessage = "Email valid";

        if (new EmailAddressAttribute().IsValid(email))
        {
            statusMessage = "Email valid";
            return true;
        }
        
        statusMessage = "Invalid email";
        return false;

    }
}