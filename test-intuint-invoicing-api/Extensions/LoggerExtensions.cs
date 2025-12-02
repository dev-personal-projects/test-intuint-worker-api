using Microsoft.Extensions.Logging;

namespace test_intuint_invoicing_api.Extensions;

/// <summary>
/// Extension methods for structured logging with correlation IDs
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Gets the correlation ID from HttpContext if available
    /// </summary>
    public static string? GetCorrelationId(this HttpContext? context)
    {
        if (context?.Items.TryGetValue("CorrelationId", out var correlationId) == true)
        {
            return correlationId?.ToString();
        }
        return null;
    }

    /// <summary>
    /// Logs with correlation ID if available in HttpContext
    /// </summary>
    public static void LogWithCorrelation(
        this ILogger logger,
        LogLevel logLevel,
        HttpContext? context,
        string message,
        params object[] args)
    {
        var correlationId = context?.GetCorrelationId();
        if (!string.IsNullOrEmpty(correlationId))
        {
            var argsWithCorrelation = new object[args.Length + 1];
            argsWithCorrelation[0] = correlationId;
            Array.Copy(args, 0, argsWithCorrelation, 1, args.Length);
            logger.Log(logLevel, $"[CorrelationId: {{CorrelationId}}] {message}", argsWithCorrelation);
        }
        else
        {
            logger.Log(logLevel, message, args);
        }
    }
}

