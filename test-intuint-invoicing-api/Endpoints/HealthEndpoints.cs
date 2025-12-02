using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using test_intuint_invoicing_api.Models;
using test_intuint_invoicing_api.Services;

namespace test_intuint_invoicing_api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", HealthCheck)
            .WithName("HealthCheck")
            .WithTags("Health")
            .Produces<ApiResponse<object>>(200);

        app.MapGet("/health/quickbooks", QuickBooksHealthCheck)
            .WithName("QuickBooksHealthCheck")
            .WithTags("Health")
            .Produces<ApiResponse<object>>(200)
            .Produces<ApiResponse<object>>(503);
    }

    private static IResult HealthCheck()
    {
        return Results.Ok(ApiResponse<object>.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
    }

    private static async Task<IResult> QuickBooksHealthCheck(
        IIntuitOAuthService oauthService,
        IHttpClientFactory httpClientFactory,
        IOptions<IntuitSettings> settings,
        ILogger<Program> logger,
        [FromQuery] string? companyId = null)
    {
        try
        {
            // Check if we have tokens for at least one company
            if (string.IsNullOrEmpty(companyId))
            {
                return Results.Ok(ApiResponse<object>.Ok(new 
                { 
                    status = "partial", 
                    message = "No companyId provided. Provide companyId to test QuickBooks connectivity.",
                    timestamp = DateTime.UtcNow 
                }));
            }

            // Check if tokens exist
            var tokens = oauthService.GetTokens(companyId);
            if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
            {
                return Results.Json(
                    ApiResponse<object>.Fail($"No OAuth tokens found for companyId: {companyId}"),
                    statusCode: 503);
            }

            // Test QuickBooks API connectivity
            var httpClient = httpClientFactory.CreateClient("QuickBooks");
            var testUrl = $"{settings.Value.BaseUrl}/v3/company/{companyId}/companyinfo/{companyId}";
            var request = new HttpRequestMessage(HttpMethod.Get, testUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.SendAsync(request);
            var isHealthy = response.IsSuccessStatusCode;

            if (isHealthy)
            {
                logger.LogInformation("QuickBooks health check passed for companyId: {CompanyId}", companyId);
                return Results.Ok(ApiResponse<object>.Ok(new 
                { 
                    status = "healthy", 
                    message = "QuickBooks API is reachable and tokens are valid",
                    companyId = companyId,
                    timestamp = DateTime.UtcNow 
                }));
            }
            else
            {
                logger.LogWarning("QuickBooks health check failed for companyId: {CompanyId}, Status: {StatusCode}", 
                    companyId, response.StatusCode);
                return Results.Json(
                    ApiResponse<object>.Fail($"QuickBooks API returned status {response.StatusCode}"),
                    statusCode: 503);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "QuickBooks health check failed with exception for companyId: {CompanyId}", companyId);
            return Results.Json(
                ApiResponse<object>.Fail($"Health check failed: {ex.Message}"),
                statusCode: 503);
        }
    }
}
