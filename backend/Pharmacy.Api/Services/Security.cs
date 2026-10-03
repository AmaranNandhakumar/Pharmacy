using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Pharmacy.Api.Services;

public static class RateLimits
{
    /// <summary>Login, refresh and password change: a few attempts per minute per client address.</summary>
    public const string Login = "login";

    public static IServiceCollection AddPharmacyRateLimits(this IServiceCollection services, IConfiguration config)
    {
        var permitLimit = int.Parse(config["RateLimiting:LoginPermitLimit"] ?? "10");
        var window = TimeSpan.FromSeconds(int.Parse(config["RateLimiting:LoginWindowSeconds"] ?? "60"));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = ((int)window.TotalSeconds).ToString();
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "Too many attempts. Wait a minute and try again." }, token);
            };
            options.AddPolicy(Login, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 }));
        });
    }
}

/// <summary>
/// Browser security headers on every response. The content security policy only allows this site's own
/// scripts, so injected script can't run; Swagger (development only) is left out because it needs inline script.
/// </summary>
public class SecurityHeadersMiddleware
{
    private const string Csp =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
        "connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        if (!context.Request.Path.StartsWithSegments("/swagger"))
            headers.ContentSecurityPolicy = Csp;

        // Patient data must not sit in shared caches or the browser's back/forward cache
        if (context.Request.Path.StartsWithSegments("/api"))
            headers.CacheControl = "no-store";

        return _next(context);
    }
}
