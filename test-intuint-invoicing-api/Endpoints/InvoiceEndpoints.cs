using Microsoft.AspNetCore.Mvc;
using test_intuint_invoicing_api.Models;
using test_intuint_invoicing_api.Services;

namespace test_intuint_invoicing_api.Endpoints;

public static class InvoiceEndpoints
{
    public static void MapInvoiceEndpoints(this WebApplication app)
    {
        app.MapPost("/api/invoices", CreateInvoice)
            .WithName("CreateInvoice")
            .WithTags("Invoices")
            .Accepts<InvoiceRequest>("application/json")
            .Produces<ApiResponse<InvoiceEntity>>(201)
            .Produces<ApiResponse<object>>(400)
            .Produces<ApiResponse<object>>(401)
            .DisableAntiforgery();

        app.MapGet("/api/invoices/{invoiceId}", GetInvoice)
            .WithName("GetInvoice")
            .WithTags("Invoices")
            .Produces<ApiResponse<InvoiceEntity>>(200)
            .Produces<ApiResponse<object>>(404);
    }

    private static async Task<IResult> CreateInvoice(
        IQuickBooksApiClient qbClient,
        IIntuitOAuthService oauthService,
        ILogger<Program> logger,
        [FromBody] InvoiceRequest? request,
        [FromQuery] string companyId)
    {
        logger.LogInformation("CreateInvoice called with companyId: {CompanyId}", companyId);
        
        // Validate request body
        if (request == null)
        {
            logger.LogWarning("CreateInvoice failed: Request body is null");
            return Results.BadRequest(ApiResponse<object>.Fail("Request body is required and must be valid JSON"));
        }
        
        logger.LogInformation("Invoice request received - CustomerRef: {CustomerRef}, Lines: {LineCount}", 
            request.CustomerRef?.Value, request.Line?.Count ?? 0);
        
        // Validate company ID is provided
        if (string.IsNullOrEmpty(companyId))
        {
            logger.LogWarning("CreateInvoice failed: companyId is required");
            return Results.BadRequest(ApiResponse<object>.Fail("companyId is required"));
        }

        // Retrieve stored OAuth tokens for this company, auto-refresh if expired
        var tokens = await oauthService.GetOrRefreshTokensAsync(companyId);
        if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            logger.LogWarning("CreateInvoice failed: No OAuth tokens found for companyId: {CompanyId}", companyId);
            var authUrl = $"/auth/authorize";
            return Results.Json(
                ApiResponse<object>.Fail($"No OAuth tokens found for companyId: {companyId}. Please complete OAuth authorization first by visiting: {authUrl}"),
                statusCode: 401);
        }

        logger.LogInformation("OAuth tokens found for companyId: {CompanyId}, creating invoice...", companyId);

        try
        {
            // Create invoice in QuickBooks using the API client
            logger.LogInformation("Calling QuickBooks API to create invoice for companyId: {CompanyId}", companyId);
            var invoice = await qbClient.CreateInvoiceAsync(companyId, tokens.AccessToken, request);
            if (invoice == null)
            {
                logger.LogError("CreateInvoice failed: QuickBooks API returned null");
                return Results.BadRequest(ApiResponse<object>.Fail("Failed to create invoice"));
            }

            logger.LogInformation("Invoice created successfully. Invoice ID: {InvoiceId}, Total: {TotalAmt}", invoice.Id, invoice.TotalAmt);
            
            // Return created invoice with 201 status
            return Results.Created($"/api/invoices/{invoice.Id}", ApiResponse<InvoiceEntity>.Ok(invoice));
        }
        catch (Exception ex)
        {
            // Return error if QuickBooks API call fails
            logger.LogError(ex, "CreateInvoice failed with exception for companyId: {CompanyId}", companyId);
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private static async Task<IResult> GetInvoice(
        IQuickBooksApiClient qbClient,
        IIntuitOAuthService oauthService,
        ILogger<Program> logger,
        string invoiceId,
        [FromQuery] string companyId)
    {
        logger.LogInformation("GetInvoice called for invoiceId: {InvoiceId}, companyId: {CompanyId}", invoiceId, companyId);
        
        // Validate company ID is provided
        if (string.IsNullOrEmpty(companyId))
            return Results.BadRequest(ApiResponse<object>.Fail("companyId is required"));

        // Retrieve stored OAuth tokens for this company, auto-refresh if expired
        var tokens = await oauthService.GetOrRefreshTokensAsync(companyId);
        if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
            return Results.Json(
                ApiResponse<object>.Fail($"No OAuth tokens found for companyId: {companyId}. Please complete OAuth authorization first by visiting: /auth/authorize"),
                statusCode: 401);

        // Fetch invoice from QuickBooks by ID
        var invoice = await qbClient.GetInvoiceAsync(companyId, tokens.AccessToken, invoiceId);
        if (invoice == null)
        {
            logger.LogWarning("Invoice not found: {InvoiceId} for companyId: {CompanyId}", invoiceId, companyId);
            return Results.NotFound(ApiResponse<object>.Fail("Invoice not found"));
        }

        logger.LogInformation("Invoice retrieved successfully: {InvoiceId}", invoiceId);
        return Results.Ok(ApiResponse<InvoiceEntity>.Ok(invoice));
    }
}

