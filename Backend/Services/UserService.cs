using System.ComponentModel.DataAnnotations;
using System.Security;
using System.Security.Cryptography;
using Backend.DTOs;
using Backend.Enums;
using Backend.Models;
using Backend.Models.User;
using Backend.Services.MongoServices;
using Backend.Models.Identity;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Backend.Services;

public class UserService(UserDbService userDbService, SessionService sessionService)
{
    public async Task<(bool IsSuccess, string Reason)> Register(RegisterDto dto)
    {
        var status = "Success";

        if (!ConfirmPasswordPolicy(dto.Password, dto.ConfirmPassword, out status)) 
            return (false, status);

        if(!ConfirmEmailPolicy(dto.Email.Trim(), out status))
            return (false, status);

        var salt = new byte[16];
        RandomNumberGenerator.Create().GetBytes(salt);
        
        var passHash = sessionService.HashPassword(dto.Password, salt);
        
        var result = await userDbService.CreateNewUserAsync(new UserModel
        {
            EUserType = EUserType.User,
            EmailVerified = false,
            Email = dto.Email.Trim(),
            PasswordHash = passHash,
            PasswordSalt = Convert.ToBase64String(salt),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            AddressData = (dto.AddressData ?? [])! // Suppress warning because the code actually works, the warning is wrong.
        });

        return (result.IsSuccess, result.Reason);
    }
    
    private static bool ConfirmPasswordPolicy(string password, string? confirmPassword, out string statusMessage)
    {
        statusMessage = "Password valid";

        if (confirmPassword is not null && password != confirmPassword)
        {
            statusMessage = "Passwords do not match";
            return false;
        }

        return true;
    }

    private static bool ConfirmEmailPolicy(string email, out string statusMessage)
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