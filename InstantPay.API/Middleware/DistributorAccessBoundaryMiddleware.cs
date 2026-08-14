using System.Security.Claims;

namespace InstantPay.API.Middleware;

/// <summary>
/// Keeps AD/MD access tokens inside their dedicated partner API surfaces.
/// </summary>
public sealed class DistributorAccessBoundaryMiddleware
{
    private readonly RequestDelegate _next;

    public DistributorAccessBoundaryMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userType = context.User.FindFirstValue("usertype");
        if (context.User.Identity?.IsAuthenticated == true &&
            (string.Equals(userType, "AD", StringComparison.Ordinal) ||
             string.Equals(userType, "MD", StringComparison.Ordinal)))
        {
            var partnerDashboard = context.Request.Path.StartsWithSegments(
                "/api/v1/partner",
                StringComparison.OrdinalIgnoreCase);
            var roleAuthSurface =
                string.Equals(userType, "AD", StringComparison.Ordinal) &&
                context.Request.Path.StartsWithSegments(
                    "/api/v1/distributor",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(userType, "MD", StringComparison.Ordinal) &&
                context.Request.Path.StartsWithSegments(
                    "/api/v1/master-distributor",
                    StringComparison.OrdinalIgnoreCase);

            if (!partnerDashboard && !roleAuthSurface)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://api.instantpayment.co.in/problems/partner-access-restricted",
                    title = "This partner account is not permitted to access this resource.",
                    status = StatusCodes.Status403Forbidden,
                    code = "PARTNER_ACCESS_RESTRICTED",
                    traceId = context.TraceIdentifier
                });
                return;
            }
        }

        await _next(context);
    }
}
