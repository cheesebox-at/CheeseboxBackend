using Microsoft.Extensions.Caching.Memory;

namespace Backend.Middleware;

public class JwtFromCookieMiddleware(RequestDelegate next, IMemoryCache cache)
{
    public async Task Invoke(HttpContext context)
    {
        
        // Check if the cookie exists
        if (context.Request.Cookies.TryGetValue("auth", out var token))
        {
            // Add the token to the Authorization header
            context.Request.Headers.Authorization = $"Bearer {token}";
        }

        // Call the next middleware
        await next(context);
    }
}