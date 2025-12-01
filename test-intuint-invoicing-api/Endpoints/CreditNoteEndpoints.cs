using Microsoft.AspNetCore.Mvc;
using test_intuint_invoicing_api.Models;
using test_intuint_invoicing_api.Services;

namespace test_intuint_invoicing_api.Endpoints;

public static class CreditNoteEndpoints
{
    public static void MapCreditNoteEndpoints(this WebApplication app)
    {
        app.MapPost("/api/invoices/{invoiceId}/credit-note", CreateCreditNote)
            .WithName("CreateCreditNote")
            .WithTags("CreditNotes")
            .Produces<ApiResponse<CreditMemoEntity>>(201)
            .Produces<ApiResponse<object>>(400);

        app.MapGet("/api/credit-notes/{creditNoteId}", GetCreditNote)
            .WithName("GetCreditNote")
            .WithTags("CreditNotes")
            .Produces<ApiResponse<CreditMemoEntity>>(200)
            .Produces<ApiResponse<object>>(404);
    }

    private static async Task<IResult> CreateCreditNote(
        IQuickBooksApiClient qbClient,
        IIntuitOAuthService oauthService,
        ILogger<Program> logger,
        string invoiceId,
        [FromQuery] string companyId)
    {
        logger.LogInformation("CreateCreditNote called for invoiceId: {InvoiceId}, companyId: {CompanyId}", invoiceId, companyId);
        
        // Validate company ID is provided
        if (string.IsNullOrEmpty(companyId))
            return Results.BadRequest(ApiResponse<object>.Fail("companyId is required"));

        // Retrieve stored OAuth tokens for this company, auto-refresh if expired
        var tokens = await oauthService.GetOrRefreshTokensAsync(companyId);
        if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
            return Results.Json(
                ApiResponse<object>.Fail($"No OAuth tokens found for companyId: {companyId}. Please complete OAuth authorization first by visiting: /auth/authorize"),
                statusCode: 401);

        try
        {
            // Fetch the original invoice to get its details
            logger.LogInformation("Fetching invoice {InvoiceId} from QuickBooks...", invoiceId);
            var invoice = await qbClient.GetInvoiceAsync(companyId, tokens.AccessToken, invoiceId);
            if (invoice == null)
            {
                logger.LogWarning("Invoice not found: {InvoiceId}", invoiceId);
                return Results.NotFound(ApiResponse<object>.Fail("Invoice not found"));
            }

            logger.LogInformation("Invoice found. Total: {TotalAmt}, creating credit memo...", invoice.TotalAmt);

            // Create credit memo request matching the invoice but with negative amounts
            // This effectively cancels the invoice
            var creditMemoRequest = new CreditMemoRequest
            {
                CustomerRef = invoice.CustomerRef,
                TxnDate = DateTime.Now.ToString("yyyy-MM-dd"),
                Line = invoice.Line?.Select(line => new LineItem
                {
                    DetailType = line.DetailType,
                    Amount = line.Amount.HasValue ? -line.Amount.Value : null,
                    Description = line.Description,
                    SalesItemLineDetail = line.SalesItemLineDetail
                }).ToList()
            };

            // Create credit memo in QuickBooks
            var creditMemo = await qbClient.CreateCreditMemoAsync(companyId, tokens.AccessToken, creditMemoRequest);
            if (creditMemo == null)
            {
                logger.LogError("Failed to create credit memo for invoice: {InvoiceId}", invoiceId);
                return Results.BadRequest(ApiResponse<object>.Fail("Failed to create credit memo"));
            }

            logger.LogInformation("Credit memo created successfully. CreditMemo ID: {CreditMemoId}, Total: {TotalAmt}", creditMemo.Id, creditMemo.TotalAmt);
            
            // Return created credit memo with 201 status
            return Results.Created($"/api/credit-notes/{creditMemo.Id}", ApiResponse<CreditMemoEntity>.Ok(creditMemo));
        }
        catch (Exception ex)
        {
            // Return error if QuickBooks API call fails
            logger.LogError(ex, "CreateCreditNote failed with exception for invoiceId: {InvoiceId}", invoiceId);
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    private static async Task<IResult> GetCreditNote(
        IQuickBooksApiClient qbClient,
        IIntuitOAuthService oauthService,
        ILogger<Program> logger,
        string creditNoteId,
        [FromQuery] string companyId)
    {
        logger.LogInformation("GetCreditNote called for creditNoteId: {CreditNoteId}, companyId: {CompanyId}", creditNoteId, companyId);
        
        // Validate company ID is provided
        if (string.IsNullOrEmpty(companyId))
            return Results.BadRequest(ApiResponse<object>.Fail("companyId is required"));

        // Retrieve stored OAuth tokens for this company, auto-refresh if expired
        var tokens = await oauthService.GetOrRefreshTokensAsync(companyId);
        if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
            return Results.Json(
                ApiResponse<object>.Fail($"No OAuth tokens found for companyId: {companyId}. Please complete OAuth authorization first by visiting: /auth/authorize"),
                statusCode: 401);

        // Fetch credit memo from QuickBooks by ID
        var creditMemo = await qbClient.GetCreditMemoAsync(companyId, tokens.AccessToken, creditNoteId);
        if (creditMemo == null)
        {
            logger.LogWarning("Credit note not found: {CreditNoteId} for companyId: {CompanyId}", creditNoteId, companyId);
            return Results.NotFound(ApiResponse<object>.Fail("Credit note not found"));
        }

        logger.LogInformation("Credit note retrieved successfully: {CreditNoteId}", creditNoteId);
        return Results.Ok(ApiResponse<CreditMemoEntity>.Ok(creditMemo));
    }
}

