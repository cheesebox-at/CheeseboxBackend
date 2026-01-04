using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Backend.DTOs;
using Backend.Models.Configuration;
using Backend.Models.User;
using Backend.Services;
using Backend.Services.MongoServices;
using Backend.Models.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using MongoDB.Bson;

namespace Backend.Endpoints;

public class SessionEndpoint
{
    public void Register(RouteGroupBuilder app)
    {
        var group = app.MapGroup("/session");

        group.MapPost("/login", async (
            [FromForm] LoginDto loginDto,
            HttpContext context, 
            IOptions<SessionConfigurationModel> sessionConfiguration, 
            SessionService sessionService, 
            UserService userService,
            UserDbService userDbService,
            RoleDbService roleDbService) =>
        {
            try
            {
                if (!await sessionService.VerifyPasswordAsync(loginDto))
                    return Results.BadRequest("Password incorrect.");
            }
            catch
            {
                return Results.InternalServerError("Failed resolving password.");
            }

            UserModel? user = null;
            
            try
            {
                user = await userDbService.GetUserAsync(loginDto.Email.Trim());
            }
            catch
            {
                return Results.InternalServerError("Failed getting user.");
            }

            if (user == null)
            {
                return Results.BadRequest("User not found.");
            }

            // Check if user is admin by checking role IDs
            bool isAdmin = false;
            if (user.RolesIds != null && user.RolesIds.Length > 0)
            {
                foreach (var roleId in user.RolesIds)
                {
                    try
                    {
                        var role = await roleDbService.GetRoleByIdAsync(roleId);
                        if (role != null && (role.Name == "SysAdmin" || role.Name == "Admin"))
                        {
                            isAdmin = true;
                            break;
                        }
                    }
                    catch
                    {
                        // Role not found, continue
                    }
                }
            }

            // Update last login timestamp
            await userDbService.UpdateLastLoginAsync(user.UserId);

            var session = await sessionService.CreateSessionAsync(user);
            
            var jwt = await sessionService.GenerateJwtTokenAsync(session.Id);

            if (jwt is null)
            {
                return Results.InternalServerError("Failed generating jwt.");
            }
            
            var jwtCookieOptions = new CookieOptions
            {
                Expires = DateTime.UtcNow + TimeSpan.FromMinutes(sessionConfiguration.Value.JwtExpireAfterMinutes),
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                IsEssential = true,
                Path = "/api" // Make sure cookie is sent for all API requests
            };
            context.Response.Cookies.Append("auth", jwt, jwtCookieOptions);

            var refreshCookieOptions = new CookieOptions
            {
                Expires = DateTime.UtcNow + TimeSpan.FromDays(sessionConfiguration.Value.ExpireAfterDays),
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Path = "/api/session/refresh",
            };
            context.Response.Cookies.Append("refresh", session.RefreshToken, refreshCookieOptions);

            // Return user object with tokens for frontend
            var response = new
            {
                user = new
                {
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    phone = user.Phone ?? string.Empty,
                    userId = user.UserId,
                    isAdmin = isAdmin
                },
                authToken = jwt,
                refreshToken = session.RefreshToken
            };
            
            return Results.Ok(response);
        }).DisableAntiforgery(); //todo check if disabling antiforgery is appropriate here

        group.MapPost("/refresh", async (
            [FromForm] ObjectId sessionId,
            IOptions<SessionConfigurationModel> sessionConfiguration,
            HttpContext context,
            SessionService sessionService
            ) =>
        {
            if (!context.Request.Cookies.TryGetValue("refresh", out var refreshToken))
            {
                return Results.Unauthorized();
            }

            var refreshTokenValid = await sessionService.ValidateRefreshTokenAsync(refreshToken, sessionId);

            if (!refreshTokenValid)
            {
                context.Response.Cookies.Delete("refresh"); //todo this doesn't seem to delete the cookie, at least not in postman
                return Results.Unauthorized();
            }

            string newRefreshToken;
            
            try
            {
                newRefreshToken = await sessionService.UpdateRefreshTokenAsync(sessionId);
            }
            catch
            {
                return Results.InternalServerError("Failed to generate new refresh token.");
            }
            
            var refreshCookieOptions = new CookieOptions
            {
                Expires = DateTime.UtcNow + TimeSpan.FromDays(sessionConfiguration.Value.ExpireAfterDays),
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Path = "/api/session/refresh",
            };
            context.Response.Cookies.Append("refresh", newRefreshToken, refreshCookieOptions);
            
            var jwt = await sessionService.GenerateJwtTokenAsync(sessionId);
            if (jwt is null)
                return Results.InternalServerError("Failed generating jwt.");
            
            var jwtCookieOptions = new CookieOptions
            {
                Expires = DateTime.UtcNow + TimeSpan.FromMinutes(sessionConfiguration.Value.JwtExpireAfterMinutes),
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                IsEssential = true,
                Path = "/api"
            };
            context.Response.Cookies.Append("auth", jwt, jwtCookieOptions);
            
            return Results.Ok();
        }).DisableAntiforgery(); //todo check if disabling antiforgery is appropriate here
        
        group.MapPost("/register", async (
            [FromForm] RegisterDto registerDto,
            HttpContext context,
            IOptions<SessionConfigurationModel> sessionConfiguration, 
            SessionService sessionService, 
            UserService userService,
            UserDbService userDbService,
            RoleDbService roleDbService
            ) =>
        {
            (bool IsSuccess, string Reason) result;
            
            try
            {
                result = await userService.Register(registerDto);
            }
            catch
            {
                return Results.InternalServerError();
            }
            
            if(!result.IsSuccess)
                return Results.BadRequest(result.Reason);

            // Get the newly created user
            UserModel? user = null;
            try
            {
                user = await userDbService.GetUserAsync(registerDto.Email.Trim());
            }
            catch
            {
                return Results.InternalServerError("Failed getting user after registration.");
            }

            if (user == null)
            {
                return Results.InternalServerError("User created but not found.");
            }

            // Check if user is admin by checking role IDs
            bool isAdmin = false;
            if (user.RolesIds != null && user.RolesIds.Length > 0)
            {
                foreach (var roleId in user.RolesIds)
                {
                    try
                    {
                        var role = await roleDbService.GetRoleByIdAsync(roleId);
                        if (role != null && (role.Name == "SysAdmin" || role.Name == "Admin"))
                        {
                            isAdmin = true;
                            break;
                        }
                    }
                    catch
                    {
                        // Role not found, continue
                    }
                }
            }

            // Create session and generate tokens
            var session = await sessionService.CreateSessionAsync(user);
            var jwt = await sessionService.GenerateJwtTokenAsync(session.Id);

            if (jwt is null)
            {
                return Results.InternalServerError("Failed generating jwt.");
            }
            
            var jwtCookieOptions = new CookieOptions
            {
                Expires = DateTime.UtcNow + TimeSpan.FromMinutes(sessionConfiguration.Value.JwtExpireAfterMinutes),
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                IsEssential = true,
                Path = "/api"
            };
            context.Response.Cookies.Append("auth", jwt, jwtCookieOptions);

            var refreshCookieOptions = new CookieOptions
            {
                Expires = DateTime.UtcNow + TimeSpan.FromDays(sessionConfiguration.Value.ExpireAfterDays),
                HttpOnly = true,
                Secure = false, // Set to true in production with HTTPS
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Path = "/api/session/refresh",
            };
            context.Response.Cookies.Append("refresh", session.RefreshToken, refreshCookieOptions);

            // Return user object with tokens for frontend
            var response = new
            {
                user = new
                {
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    phone = user.Phone ?? string.Empty,
                    userId = user.UserId,
                    isAdmin = isAdmin
                },
                authToken = jwt,
                refreshToken = session.RefreshToken
            };
            
            return Results.Ok(response);
        }).DisableAntiforgery(); //todo check if disabling antiforgery is appropriate here

        // Google OAuth login/register endpoint
        group.MapPost("/google", async (
            [FromBody] GoogleLoginDto googleLoginDto,
            HttpContext context,
            IOptions<SessionConfigurationModel> sessionConfiguration,
            SessionService sessionService,
            UserDbService userDbService,
            RoleDbService roleDbService,
            GoogleAuthService googleAuthService
        ) =>
        {
            try
            {
                // Validate the Google ID token
                var googleUserInfo = await googleAuthService.ValidateIdTokenAsync(googleLoginDto.IdToken);

                if (googleUserInfo == null)
                {
                    return Results.BadRequest("Invalid Google token");
                }

                // Try to find existing user by Google ID
                var user = await userDbService.TryGetUserByGoogleIdAsync(googleUserInfo.GoogleId);

                if (user == null)
                {
                    // Check if a user with this email already exists (registered with password)
                    var existingUser = await userDbService.TryGetUserByEmailAsync(googleUserInfo.Email);

                    if (existingUser != null)
                    {
                        // Link Google account to existing user
                        await userDbService.LinkGoogleAccountAsync(
                            existingUser.UserId,
                            googleUserInfo.GoogleId,
                            googleUserInfo.ProfilePictureUrl
                        );
                        user = await userDbService.TryGetUserByIdAsync(existingUser.UserId);
                    }
                    else
                    {
                        // Create new user from Google data
                        var createResult = await userDbService.CreateGoogleUserAsync(
                            googleUserInfo.GoogleId,
                            googleUserInfo.Email,
                            googleUserInfo.FirstName,
                            googleUserInfo.LastName,
                            googleUserInfo.ProfilePictureUrl,
                            googleUserInfo.EmailVerified
                        );

                        if (!createResult.IsSuccess || createResult.User == null)
                        {
                            return Results.BadRequest(createResult.Reason);
                        }

                        user = createResult.User;
                    }
                }

                if (user == null)
                {
                    return Results.InternalServerError("Failed to create or find user");
                }

                // Check if user is admin
                bool isAdmin = false;
                if (user.RolesIds != null && user.RolesIds.Length > 0)
                {
                    foreach (var roleId in user.RolesIds)
                    {
                        try
                        {
                            var role = await roleDbService.GetRoleByIdAsync(roleId);
                            if (role != null && (role.Name == "SysAdmin" || role.Name == "Admin"))
                            {
                                isAdmin = true;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                // Update last login
                await userDbService.UpdateLastLoginAsync(user.UserId);

                // Create session and generate tokens
                var session = await sessionService.CreateSessionAsync(user);
                var jwt = await sessionService.GenerateJwtTokenAsync(session.Id);

                if (jwt is null)
                {
                    return Results.InternalServerError("Failed generating jwt.");
                }

                // Set cookies
                var jwtCookieOptions = new CookieOptions
                {
                    Expires = DateTime.UtcNow + TimeSpan.FromMinutes(sessionConfiguration.Value.JwtExpireAfterMinutes),
                    HttpOnly = true,
                    Secure = false, // Set to true in production with HTTPS
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                    IsEssential = true,
                    Path = "/api"
                };
                context.Response.Cookies.Append("auth", jwt, jwtCookieOptions);

                var refreshCookieOptions = new CookieOptions
                {
                    Expires = DateTime.UtcNow + TimeSpan.FromDays(sessionConfiguration.Value.ExpireAfterDays),
                    HttpOnly = true,
                    Secure = false,
                    SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                    Path = "/api/session/refresh",
                };
                context.Response.Cookies.Append("refresh", session.RefreshToken, refreshCookieOptions);

                // Return user object with tokens
                var response = new
                {
                    user = new
                    {
                        email = user.Email,
                        firstName = user.FirstName,
                        lastName = user.LastName,
                        phone = user.Phone ?? string.Empty,
                        userId = user.UserId,
                        isAdmin = isAdmin,
                        profilePictureUrl = user.ProfilePictureUrl,
                        authProvider = user.AuthProvider ?? "google"
                    },
                    authToken = jwt,
                    refreshToken = session.RefreshToken
                };

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google login error: {ex.Message}");
                return Results.InternalServerError("Google authentication failed");
            }
        });
    }
}