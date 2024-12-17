using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
            UserDbService userDbService) =>
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
                Secure = true,
                IsEssential = true // todo this can maybe be removed
            };
            context.Response.Cookies.Append("auth", jwt, jwtCookieOptions);

            var refreshCookieOptions = new CookieOptions
            {
                Expires = DateTime.UtcNow + TimeSpan.FromDays(sessionConfiguration.Value.ExpireAfterDays),
                HttpOnly = true,
                Secure = true,
                Path = "/api/session/refresh",
            };
            context.Response.Cookies.Append("refresh", session.RefreshToken, refreshCookieOptions);
            
            return Results.Ok();
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
                Secure = true,
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
                Secure = true,
                IsEssential = true // todo this can maybe be removed
            };
            context.Response.Cookies.Append("auth", jwt, jwtCookieOptions);
            
            return Results.Ok();
        }).DisableAntiforgery(); //todo check if disabling antiforgery is appropriate here
        
        group.MapPost("/register", async (
            [FromForm] RegisterDto registerDto,
            HttpContext context, 
            UserService userService
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
            
            if(result.IsSuccess)
                return Results.Ok(result.Reason);
            
            return Results.BadRequest(result.Reason);
        }).DisableAntiforgery(); //todo check if disabling antiforgery is appropriate here
    }
}