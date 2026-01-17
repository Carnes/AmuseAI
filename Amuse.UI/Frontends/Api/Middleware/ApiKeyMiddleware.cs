using System.Threading.Tasks;
using Amuse.UI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Amuse.UI.Frontends.Api.Middleware
{
    /// <summary>
    /// Middleware that validates API key from X-API-Key header.
    /// If ApiKey is not configured in settings, all requests are allowed.
    /// </summary>
    public class ApiKeyMiddleware
    {
        private const string ApiKeyHeaderName = "X-API-Key";
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiKeyMiddleware> _logger;

        public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, AmuseSettings settings)
        {
            // Skip authentication if no API key is configured
            if (string.IsNullOrEmpty(settings.ApiKey))
            {
                await _next(context);
                return;
            }

            // Allow Swagger UI and OpenAPI spec without authentication
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            if (path.StartsWith("/swagger"))
            {
                await _next(context);
                return;
            }

            // Check for API key in header
            if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey))
            {
                _logger.LogWarning("[API] Request rejected: Missing {Header} header", ApiKeyHeaderName);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"API key required\",\"code\":\"UNAUTHORIZED\"}");
                return;
            }

            // Validate API key
            if (!string.Equals(providedKey, settings.ApiKey))
            {
                _logger.LogWarning("[API] Request rejected: Invalid API key");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Invalid API key\",\"code\":\"UNAUTHORIZED\"}");
                return;
            }

            await _next(context);
        }
    }
}
