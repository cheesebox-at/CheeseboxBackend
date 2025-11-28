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
    public async Task<(bool IsSuccess, string Reason)> CreateNewUserAsync(UserModel user)
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
                        var sysAdminRole = await roleDbService.GetRoleByIdAsync(0);
                    }
                    catch (InvalidOperationException ex)
                    {
                        try
                        {
                            await roleDbService.CreateRoleAsync(new RoleModel());
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

    /// <summary>
    /// Gets all users from the database
    /// </summary>
    /// <param name="skip">Number of records to skip (for pagination)</param>
    /// <param name="limit">Maximum number of records to return</param>
    /// <returns>List of users</returns>
    public async Task<List<UserModel>> GetAllUsersAsync(int skip = 0, int limit = 100)
    {
        var filter = Builders<UserModel>.Filter.Empty;
        var options = new FindOptions<UserModel>
        {
            Skip = skip,
            Limit = limit,
            Sort = Builders<UserModel>.Sort.Descending(x => x.UserId)
        };
        
        var users = await (await userCollection.FindAsync(filter, options)).ToListAsync();
        return users;
    }

    /// <summary>
    /// Gets the total count of users
    /// </summary>
    /// <returns>Total number of users</returns>
    public async Task<long> GetUserCountAsync()
    {
        return await userCollection.CountDocumentsAsync(Builders<UserModel>.Filter.Empty);
    }

    /// <summary>
    /// Tries to get a user by email, returns null if not found
    /// </summary>
    /// <param name="email"></param>
    /// <returns>User or null</returns>
    public async Task<UserModel?> TryGetUserByEmailAsync(string email)
    {
        try
        {
            var filter = Builders<UserModel>.Filter.Eq(x => x.Email, email);
            var user = await (await userCollection.FindAsync(filter)).FirstOrDefaultAsync();
            return user;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to get a user by ID, returns null if not found
    /// </summary>
    /// <param name="userId"></param>
    /// <returns>User or null</returns>
    public async Task<UserModel?> TryGetUserByIdAsync(long userId)
    {
        try
        {
            var filter = Builders<UserModel>.Filter.Eq(x => x.UserId, userId);
            var user = await (await userCollection.FindAsync(filter)).FirstOrDefaultAsync();
            return user;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Search users by name or email
    /// </summary>
    /// <param name="searchQuery">Search query</param>
    /// <param name="skip">Number of records to skip</param>
    /// <param name="limit">Maximum number of records</param>
    /// <returns>List of matching users</returns>
    public async Task<List<UserModel>> SearchUsersAsync(string searchQuery, int skip = 0, int limit = 100)
    {
        var filter = Builders<UserModel>.Filter.Or(
            Builders<UserModel>.Filter.Regex(x => x.Email, new MongoDB.Bson.BsonRegularExpression(searchQuery, "i")),
            Builders<UserModel>.Filter.Regex(x => x.FirstName, new MongoDB.Bson.BsonRegularExpression(searchQuery, "i")),
            Builders<UserModel>.Filter.Regex(x => x.LastName, new MongoDB.Bson.BsonRegularExpression(searchQuery, "i"))
        );
        
        var options = new FindOptions<UserModel>
        {
            Skip = skip,
            Limit = limit,
            Sort = Builders<UserModel>.Sort.Descending(x => x.UserId)
        };
        
        var users = await (await userCollection.FindAsync(filter, options)).ToListAsync();
        return users;
    }

    /// <summary>
    /// Updates the user's last login timestamp
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>True if update successful</returns>
    public async Task<bool> UpdateLastLoginAsync(long userId)
    {
        try
        {
            var filter = Builders<UserModel>.Filter.Eq(x => x.UserId, userId);
            var update = Builders<UserModel>.Update
                .Set(x => x.UserMetrics.LastLogin, DateTime.UtcNow)
                .Set(x => x.UserMetrics.LastActivity, DateTime.UtcNow);
            
            var result = await userCollection.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update last login for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Updates user profile data
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="firstName">First name</param>
    /// <param name="lastName">Last name</param>
    /// <param name="phone">Phone number</param>
    /// <param name="street">Street</param>
    /// <param name="houseNumber">House number</param>
    /// <param name="postalCode">Postal code</param>
    /// <param name="city">City</param>
    /// <returns>True if update successful</returns>
    public async Task<bool> UpdateUserProfileAsync(
        long userId, 
        string? firstName, 
        string? lastName, 
        string? phone,
        string? street,
        string? houseNumber,
        string? postalCode,
        string? city)
    {
        try
        {
            var filter = Builders<UserModel>.Filter.Eq(x => x.UserId, userId);
            var updateDefinitions = new List<UpdateDefinition<UserModel>>();

            if (firstName != null)
                updateDefinitions.Add(Builders<UserModel>.Update.Set(x => x.FirstName, firstName));
            if (lastName != null)
                updateDefinitions.Add(Builders<UserModel>.Update.Set(x => x.LastName, lastName));
            if (phone != null)
                updateDefinitions.Add(Builders<UserModel>.Update.Set(x => x.Phone, phone));
            
            // Update metrics
            updateDefinitions.Add(Builders<UserModel>.Update.Set(x => x.UserMetrics.LastUpdate, DateTime.UtcNow));
            updateDefinitions.Add(Builders<UserModel>.Update.Set(x => x.UserMetrics.LastActivity, DateTime.UtcNow));

            // Handle address data - create or update primary address
            if (street != null || houseNumber != null || postalCode != null || city != null)
            {
                var user = await TryGetUserByIdAsync(userId);
                if (user != null)
                {
                    var addresses = user.AddressData?.ToList() ?? new List<AddressModel>();
                    
                    if (addresses.Count > 0)
                    {
                        // Update first address
                        if (street != null) addresses[0].Street = street + (houseNumber != null ? " " + houseNumber : "");
                        if (postalCode != null) addresses[0].ZipCode = postalCode;
                        if (city != null) addresses[0].City = city;
                    }
                    else
                    {
                        // Create new address
                        addresses.Add(new AddressModel
                        {
                            FirstName = firstName ?? user.FirstName,
                            LastName = lastName ?? user.LastName,
                            Street = (street ?? "") + (houseNumber != null ? " " + houseNumber : ""),
                            ZipCode = postalCode ?? "",
                            City = city ?? "",
                            State = "",
                            Country = "Austria"
                        });
                    }
                    
                    updateDefinitions.Add(Builders<UserModel>.Update.Set(x => x.AddressData, addresses.ToArray()));
                }
            }

            if (updateDefinitions.Count == 0)
                return true;

            var update = Builders<UserModel>.Update.Combine(updateDefinitions);
            var result = await userCollection.UpdateOneAsync(filter, update);
            
            logger.LogInformation("Updated user profile for user {UserId}", userId);
            return result.ModifiedCount > 0 || result.MatchedCount > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update profile for user {UserId}", userId);
            return false;
        }
    }

    /// <summary>
    /// Updates user password
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="newPasswordHash">New password hash</param>
    /// <param name="newPasswordSalt">New password salt</param>
    /// <returns>True if update successful</returns>
    public async Task<bool> UpdatePasswordAsync(long userId, string newPasswordHash, string newPasswordSalt)
    {
        try
        {
            var filter = Builders<UserModel>.Filter.Eq(x => x.UserId, userId);
            var update = Builders<UserModel>.Update
                .Set(x => x.PasswordHash, newPasswordHash)
                .Set(x => x.PasswordSalt, newPasswordSalt)
                .Set(x => x.UserMetrics.LastUpdate, DateTime.UtcNow)
                .Set(x => x.UserMetrics.LastActivity, DateTime.UtcNow);
            
            var result = await userCollection.UpdateOneAsync(filter, update);
            logger.LogInformation("Updated password for user {UserId}", userId);
            return result.ModifiedCount > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update password for user {UserId}", userId);
            return false;
        }
    }

}