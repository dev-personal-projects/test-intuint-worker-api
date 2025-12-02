using System.Text.Json;
using test_intuint_invoicing_api.Models;
using test_intuint_invoicing_api.Services;

namespace test_intuint_invoicing_api.Middleware;

/// <summary>
/// Middleware to validate common request requirements (companyId, OAuth tokens)
/// </summary>
public class RequestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestValidationMiddleware> _logger;
    private readonly HashSet<string> _excludedPaths;

    public RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        
        // Paths that don't require companyId validation (auth endpoints, health checks)
        _excludedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/auth/authorize",
            "/auth/callback",
            "/auth/refresh",
            "/health"
        };
    }

    public async Task InvokeAsync(HttpContext context, IIntuitOAuthService oauthService)
    {
        var path = context.Request.Path.Value ?? "";
        
        // Skip validation for excluded paths
        if (_excludedPaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Validate companyId query parameter for API endpoints
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            var companyId = context.Request.Query["companyId"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(companyId))
            {
                _logger.LogWarning("Request to {Path} missing companyId parameter", path);
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                
                var errorResponse = ApiResponse<object>.Fail("companyId query parameter is required");
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return;
            }

            // Check if OAuth tokens exist for this company
            // Note: We don't validate token expiration here as that's handled by GetOrRefreshTokensAsync
            var tokens = oauthService.GetTokens(companyId);
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
            {
                _logger.LogWarning("Request to {Path} for companyId {CompanyId} has no OAuth tokens", path, companyId);
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                
                var errorResponse = ApiResponse<object>.Fail(
                    $"No OAuth tokens found for companyId: {companyId}. Please complete OAuth authorization first by visiting: /auth/authorize");
                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return;
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to register the request validation middleware
/// </summary>
public static class RequestValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestValidationMiddleware>();
    }
}

