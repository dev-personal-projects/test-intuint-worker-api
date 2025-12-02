using System.Diagnostics;

namespace test_intuint_invoicing_api.Helpers;

/// <summary>
/// Helper class for retry logic with exponential backoff
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Retries an async operation with exponential backoff
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The async operation to retry</param>
    /// <param name="maxAttempts">Maximum number of retry attempts (default: 5)</param>
    /// <param name="initialDelayMs">Initial delay in milliseconds (default: 200)</param>
    /// <param name="maxDelayMs">Maximum delay in milliseconds (default: 2000)</param>
    /// <param name="shouldRetry">Optional predicate to determine if retry should occur (default: always retry)</param>
    /// <param name="onRetry">Optional callback for logging retry attempts</param>
    /// <returns>The result of the operation</returns>
    public static async Task<T> RetryWithExponentialBackoffAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 5,
        int initialDelayMs = 200,
        int maxDelayMs = 2000,
        Func<Exception, bool>? shouldRetry = null,
        Action<int, Exception, TimeSpan>? onRetry = null)
    {
        shouldRetry ??= _ => true;
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < maxAttempts)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (shouldRetry(ex) && attempt < maxAttempts - 1)
            {
                lastException = ex;
                attempt++;
                
                // Calculate exponential backoff delay
                var delayMs = Math.Min(initialDelayMs * (int)Math.Pow(2, attempt - 1), maxDelayMs);
                var delay = TimeSpan.FromMilliseconds(delayMs);
                
                onRetry?.Invoke(attempt, ex, delay);
                await Task.Delay(delay);
            }
        }

        // If we get here, all retries failed
        throw lastException ?? new InvalidOperationException("Operation failed after all retry attempts");
    }

    /// <summary>
    /// Polls an async operation until a condition is met or timeout is reached
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The async operation to poll</param>
    /// <param name="condition">Condition that must be met to stop polling</param>
    /// <param name="maxAttempts">Maximum number of polling attempts (default: 10)</param>
    /// <param name="initialDelayMs">Initial delay in milliseconds (default: 200)</param>
    /// <param name="maxDelayMs">Maximum delay in milliseconds (default: 2000)</param>
    /// <param name="onPoll">Optional callback for logging poll attempts</param>
    /// <returns>The result of the operation when condition is met</returns>
    public static async Task<T> PollUntilConditionAsync<T>(
        Func<Task<T>> operation,
        Func<T, bool> condition,
        int maxAttempts = 10,
        int initialDelayMs = 200,
        int maxDelayMs = 2000,
        Action<int, T>? onPoll = null)
    {
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            var result = await operation();
            onPoll?.Invoke(attempt + 1, result);

            if (condition(result))
            {
                return result;
            }

            attempt++;
            
            if (attempt < maxAttempts)
            {
                // Calculate exponential backoff delay
                var delayMs = Math.Min(initialDelayMs * (int)Math.Pow(2, attempt - 1), maxDelayMs);
                await Task.Delay(delayMs);
            }
        }

        throw new TimeoutException($"Polling condition not met after {maxAttempts} attempts");
    }
}

