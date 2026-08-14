using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.API.Middleware;

public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        // Skip endpoints marked [AllowAnonymous] (login, verifyotp, logout, etc.)
        // Allow anonymous access to static file paths (e.g. APK downloads)
        if (context.Request.Path.StartsWithSegments("/UploadFiles", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
        {
            await _next(context);
            return;
        }

        var authenticatedUserType = context.User.FindFirst("usertype")?.Value;
        if (context.User.Identity?.IsAuthenticated == true &&
            (authenticatedUserType == "AD" || authenticatedUserType == "MD") &&
            context.Request.Path.StartsWithSegments(
                "/api/v1/partner",
                StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var userIdHeader = context.Request.Headers["userid"].FirstOrDefault();
        var username = context.Request.Headers["username"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(userIdHeader) || !int.TryParse(userIdHeader, out int userId) || userId <= 0)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"userid header is missing or invalid.\"}");
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"username header is missing or invalid.\"}");
            return;
        }

        var platform = context.Request.Headers["platform"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(platform))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"platform header is required.\"}");
            return;
        }

        if (!string.Equals(platform, "web", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(platform, "apk", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"Invalid platform. Allowed values: web, apk.\"}");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.TblUsers
            .AsNoTracking()
            .Where(u => u.Id == userId && u.Username == username)
            .Select(u => new { u.IsUserLoggedInFromWeb, u.IsUserLoggedInFromApk, u.LockoutEnd })
            .FirstOrDefaultAsync();

        if (user == null)
        {
            var superAdmin = await db.TblSuperadmins
                .AsNoTracking()
                .Where(s => s.Id == userId && s.Username == username)
                .Select(s => new { s.Id })
                .FirstOrDefaultAsync();

            if (superAdmin == null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"message\":\"Invalid userid or username.\"}");
                return;
            }

            await _next(context);
            return;
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"Your account is locked. Please try again later or contact admin.\"}");
            return;
        }

        bool isLoggedIn;

        if (string.Equals(platform, "web", StringComparison.OrdinalIgnoreCase))
        {
            isLoggedIn = user.IsUserLoggedInFromWeb == true;
        }
        else
        {
            isLoggedIn = user.IsUserLoggedInFromApk == true;
        }

        if (!isLoggedIn)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"Session expired. Please login again.\"}");
            return;
        }

        await _next(context);
    }
}
