using System.Diagnostics;

namespace test_intuint_invoicing_api.Middleware;

/// <summary>
/// Middleware to add correlation ID to requests for better traceability
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string CorrelationIdKey = "CorrelationId";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get correlation ID from request header or generate a new one
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault() 
            ?? Guid.NewGuid().ToString();

        // Add correlation ID to response headers
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        // Add correlation ID to HttpContext items for use in logging
        context.Items[CorrelationIdKey] = correlationId;

        // Add correlation ID to Activity (for distributed tracing if configured)
        Activity.Current?.SetTag("correlation.id", correlationId);

        await _next(context);
    }
}

/// <summary>
/// Extension method to register the correlation ID middleware
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}

