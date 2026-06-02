namespace InstantPay.API.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Server"] = string.Empty;
            context.Response.Headers.Remove("Server");
            context.Response.Headers["X-Powered-By"] = string.Empty;
            context.Response.Headers.Remove("X-Powered-By");
            context.Response.Headers.Remove("X-AspNet-Version");
            context.Response.Headers.Remove("X-AspNetMvc-Version");
            context.Response.Headers.Remove("X-SourceFiles");
            context.Response.Headers.Remove("X-Runtime");
            context.Response.Headers.Remove("X-Generator");

            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            context.Response.Headers["Content-Security-Policy"] = BuildCsp(context);

            SetCacheHeaders(context);

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private void SetCacheHeaders(HttpContext context)
    {
        bool isSwaggerStaticAsset = _env.IsDevelopment()
            && context.Request.Path.StartsWithSegments("/swagger")
            && !context.Request.Path.StartsWithSegments("/swagger/v1/swagger.json");

        if (isSwaggerStaticAsset)
        {
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Pragma"] = "no-cache";
        }
        else
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
        }
    }

    private string BuildCsp(HttpContext context)
    {
        // Swagger UI requires inline scripts/styles; only reachable in Development
        if (_env.IsDevelopment() && context.Request.Path.StartsWithSegments("/swagger"))
        {
            return "default-src 'self'; " +
                   "script-src 'self' 'unsafe-inline'; " +
                   "style-src 'self' 'unsafe-inline'; " +
                   "img-src 'self' data:; " +
                   "font-src 'self'; " +
                   "connect-src 'self'; " +
                   "worker-src blob:; " +
                   "frame-ancestors 'none'; " +
                   "base-uri 'self'; " +
                   "form-action 'self'";
        }

        // Strict policy for all API endpoints and production — no content rendering expected
        return "default-src 'none'; " +
               "frame-ancestors 'none'; " +
               "base-uri 'none'; " +
               "form-action 'none'";
    }
}
