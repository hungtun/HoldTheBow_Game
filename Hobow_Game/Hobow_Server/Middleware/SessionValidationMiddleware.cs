using Hobow_Server.Models;
using Microsoft.EntityFrameworkCore;

namespace Hobow_Server.Middleware;

public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    public SessionValidationMiddleware(RequestDelegate next, ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, GameDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst("id")?.Value;
            var sessionIdClaim = context.User.FindFirst("sessionId")?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && !string.IsNullOrEmpty(sessionIdClaim))
            {
                var userId = int.Parse(userIdClaim);
                
                var user = await dbContext.Users.FindAsync(userId);
                if (user == null || user.ActiveSessionId != sessionIdClaim)
                {
                    _logger.LogWarning("Session validation failed for user {UserId}. Expected: {ExpectedSessionId}, Actual: {ActualSessionId}", 
                        userId, user?.ActiveSessionId, sessionIdClaim);
                    
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Session invalid. Please login again.");
                    return;
                }
                
                _logger.LogDebug("Session validation successful for user {UserId}", userId);
            }
        }

        await _next(context);
    }
}
