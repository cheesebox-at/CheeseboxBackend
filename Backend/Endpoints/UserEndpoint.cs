using Backend.DTOs;
using Backend.Enums;
using Backend.Models.User;
using Backend.Services;
using Backend.Services.MongoServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace Backend.Endpoints;

public class UserEndpoint
{
    public void Register(RouteGroupBuilder app)
    {
        var group = app.MapGroup("/user");

        // Get all users (Admin only)
        group.MapGet("/getAll", [Authorize] async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            UserDbService userDbService,
            OrderDbService orderDbService) =>
        {
            try
            {
                var skip = (page - 1) * pageSize;
                var limit = Math.Min(pageSize, 100); // Max 100 per page

                List<UserModel> users;
                long totalCount;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    users = await userDbService.SearchUsersAsync(search, skip, limit);
                    // For search, we'd need a separate count method, but for now use the list count
                    totalCount = users.Count;
                }
                else
                {
                    users = await userDbService.GetAllUsersAsync(skip, limit);
                    totalCount = await userDbService.GetUserCountAsync();
                }

                // Get order statistics for each user
                var userDtos = new List<UserDto>();
                foreach (var user in users)
                {
                    var userOrders = await orderDbService.GetOrdersByUserIdAsync(user.UserId);
                    var completedOrders = userOrders.Where(o => o.Status != EOrderStatus.Cancelled).ToList();

                    userDtos.Add(MapToDto(user, userOrders.Count, 
                        completedOrders.Sum(o => o.TotalPrice)));
                }

                var response = new UserListResponseDto
                {
                    Users = userDtos,
                    TotalCount = (int)totalCount,
                    Page = page,
                    PageSize = pageSize
                };

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.InternalServerError($"Error fetching users: {ex.Message}");
            }
        });

        // Get user by ID
        group.MapGet("/getById/{userId:long}", [Authorize] async (
            long userId,
            UserDbService userDbService,
            OrderDbService orderDbService) =>
        {
            try
            {
                var user = await userDbService.TryGetUserByIdAsync(userId);
                
                if (user == null)
                {
                    return Results.NotFound($"User with ID {userId} not found");
                }

                var userOrders = await orderDbService.GetOrdersByUserIdAsync(userId);
                var completedOrders = userOrders.Where(o => o.Status != EOrderStatus.Cancelled).ToList();

                var userDto = MapToDetailDto(user, userOrders.Count, 
                    completedOrders.Sum(o => o.TotalPrice));

                return Results.Ok(userDto);
            }
            catch (Exception ex)
            {
                return Results.InternalServerError($"Error fetching user: {ex.Message}");
            }
        });

        // Get user by email
        group.MapGet("/getByEmail/{email}", [Authorize] async (
            string email,
            UserDbService userDbService,
            OrderDbService orderDbService) =>
        {
            try
            {
                var decodedEmail = Uri.UnescapeDataString(email);
                var user = await userDbService.TryGetUserByEmailAsync(decodedEmail);
                
                if (user == null)
                {
                    return Results.NotFound($"User with email {decodedEmail} not found");
                }

                var userOrders = await orderDbService.GetOrdersByUserIdAsync(user.UserId);
                var completedOrders = userOrders.Where(o => o.Status != EOrderStatus.Cancelled).ToList();

                var userDto = MapToDetailDto(user, userOrders.Count, 
                    completedOrders.Sum(o => o.TotalPrice));

                return Results.Ok(userDto);
            }
            catch (Exception ex)
            {
                return Results.InternalServerError($"Error fetching user: {ex.Message}");
            }
        });

        // Get current logged-in user's profile
        group.MapGet("/me", [Authorize] async (
            HttpContext context,
            UserDbService userDbService,
            OrderDbService orderDbService) =>
        {
            try
            {
                var userIdClaim = context.User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
                {
                    return Results.Unauthorized();
                }

                var user = await userDbService.TryGetUserByIdAsync(userId);
                
                if (user == null)
                {
                    return Results.NotFound("User not found");
                }

                var userOrders = await orderDbService.GetOrdersByUserIdAsync(userId);
                var completedOrders = userOrders.Where(o => o.Status != EOrderStatus.Cancelled).ToList();

                var userDto = MapToDetailDto(user, userOrders.Count, 
                    completedOrders.Sum(o => o.TotalPrice));

                return Results.Ok(userDto);
            }
            catch (Exception ex)
            {
                return Results.InternalServerError($"Error fetching user profile: {ex.Message}");
            }
        });

        // Get user statistics summary (Admin only)
        group.MapGet("/statistics", [Authorize] async (
            UserDbService userDbService,
            OrderDbService orderDbService) =>
        {
            try
            {
                var totalUsers = await userDbService.GetUserCountAsync();
                var allOrders = await orderDbService.GetAllOrdersAsync();
                
                // Get unique users with orders
                var usersWithOrders = allOrders
                    .Select(o => o.UserId)
                    .Distinct()
                    .Count();

                var stats = new
                {
                    TotalUsers = totalUsers,
                    UsersWithOrders = usersWithOrders,
                    TotalOrders = allOrders.Count,
                    TotalRevenue = allOrders
                        .Where(o => o.Status != EOrderStatus.Cancelled)
                        .Sum(o => o.TotalPrice)
                };

                return Results.Ok(stats);
            }
            catch (Exception ex)
            {
                return Results.InternalServerError($"Error fetching statistics: {ex.Message}");
            }
        });

        // Update current user's profile
        group.MapPut("/update", [Authorize] async (
            HttpContext context,
            [FromBody] UpdateUserProfileDto updateDto,
            UserDbService userDbService) =>
        {
            try
            {
                var userIdClaim = context.User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
                {
                    return Results.Unauthorized();
                }

                var success = await userDbService.UpdateUserProfileAsync(
                    userId,
                    updateDto.FirstName,
                    updateDto.LastName,
                    updateDto.Phone,
                    updateDto.Street,
                    updateDto.HouseNumber,
                    updateDto.PostalCode,
                    updateDto.City
                );

                if (success)
                {
                    // Return updated user data
                    var user = await userDbService.TryGetUserByIdAsync(userId);
                    if (user != null)
                    {
                        return Results.Ok(new
                        {
                            success = true,
                            message = "Profile updated successfully",
                            user = new
                            {
                                userId = user.UserId,
                                email = user.Email,
                                firstName = user.FirstName,
                                lastName = user.LastName,
                                phone = user.Phone,
                                street = user.AddressData?.FirstOrDefault()?.Street ?? "",
                                postalCode = user.AddressData?.FirstOrDefault()?.ZipCode ?? "",
                                city = user.AddressData?.FirstOrDefault()?.City ?? ""
                            }
                        });
                    }
                }

                return Results.BadRequest("Failed to update profile");
            }
            catch (Exception ex)
            {
                return Results.InternalServerError($"Error updating profile: {ex.Message}");
            }
        });

        // Change password
        group.MapPost("/change-password", [Authorize] async (
            HttpContext context,
            [FromBody] ChangePasswordDto passwordDto,
            UserDbService userDbService,
            SessionService sessionService) =>
        {
            try
            {
                var userIdClaim = context.User.FindFirst("userId")?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
                {
                    return Results.Unauthorized();
                }

                // Get user
                var user = await userDbService.TryGetUserByIdAsync(userId);
                if (user == null)
                {
                    return Results.NotFound("User not found");
                }

                // Verify old password
                var oldPasswordHash = sessionService.HashPassword(passwordDto.OldPassword, Convert.FromBase64String(user.PasswordSalt));
                if (oldPasswordHash != user.PasswordHash)
                {
                    return Results.BadRequest(new { success = false, message = "Aktuelles Passwort ist falsch" });
                }

                // Validate new password
                if (string.IsNullOrWhiteSpace(passwordDto.NewPassword) || passwordDto.NewPassword.Length < 6)
                {
                    return Results.BadRequest(new { success = false, message = "Das neue Passwort muss mindestens 6 Zeichen lang sein" });
                }

                if (passwordDto.NewPassword != passwordDto.ConfirmPassword)
                {
                    return Results.BadRequest(new { success = false, message = "Die Passwörter stimmen nicht überein" });
                }

                // Generate new salt and hash
                var newSalt = new byte[16];
                RandomNumberGenerator.Create().GetBytes(newSalt);
                var newPasswordHash = sessionService.HashPassword(passwordDto.NewPassword, newSalt);

                // Update password
                var success = await userDbService.UpdatePasswordAsync(userId, newPasswordHash, Convert.ToBase64String(newSalt));

                if (success)
                {
                    return Results.Ok(new { success = true, message = "Passwort erfolgreich geändert" });
                }

                return Results.BadRequest(new { success = false, message = "Fehler beim Ändern des Passworts" });
            }
            catch (Exception ex)
            {
                return Results.InternalServerError($"Error changing password: {ex.Message}");
            }
        });
    }

    private static UserDto MapToDto(UserModel user, int totalOrders, decimal totalSpent)
    {
        return new UserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            EmailVerified = user.EmailVerified,
            UserType = user.EUserType.ToString(),
            CreatedAt = user.UserMetrics?.RegisteredAt ?? DateTime.MinValue,
            LastLoginAt = user.UserMetrics?.LastLogin,
            TotalOrders = totalOrders,
            TotalSpent = totalSpent
        };
    }

    private static UserDetailDto MapToDetailDto(UserModel user, int totalOrders, decimal totalSpent)
    {
        return new UserDetailDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            EmailVerified = user.EmailVerified,
            UserType = user.EUserType.ToString(),
            CreatedAt = user.UserMetrics?.RegisteredAt ?? DateTime.MinValue,
            LastLoginAt = user.UserMetrics?.LastLogin,
            TotalOrders = totalOrders,
            TotalSpent = totalSpent,
            Addresses = user.AddressData?.Select(a => new AddressDto
            {
                FirstName = a.FirstName,
                LastName = a.LastName,
                Street = a.Street,
                City = a.City,
                ZipCode = a.ZipCode,
                State = a.State,
                Country = a.Country
            }).ToArray() ?? [],
            Metrics = new UserMetricsDto
            {
                LastLoginAt = user.UserMetrics?.LastLogin,
                CreatedAt = user.UserMetrics?.RegisteredAt ?? DateTime.MinValue,
                LoginCount = 0 // Could be added to UserMetrics if needed
            }
        };
    }
}

