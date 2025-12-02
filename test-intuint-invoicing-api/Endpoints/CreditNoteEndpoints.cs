using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using test_intuint_invoicing_api.Extensions;
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

        app.MapGet("/api/credit-notes", GetAllCreditNotes)
            .WithName("GetAllCreditNotes")
            .WithTags("CreditNotes")
            .Produces<ApiResponse<List<CreditMemoEntity>>>(200)
            .Produces<ApiResponse<object>>(401);

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

            // Create credit memo request matching the invoice
            // QuickBooks credit memos use positive amounts - they're automatically treated as credits
            // QuickBooks requires all amounts to be positive (0 or greater)
            // Credit memos are automatically treated as credits by QuickBooks
            // We use the same positive amounts as the invoice - QuickBooks handles the credit nature
            var salesItemLines = invoice.Line?.GetSalesItemLineDetails() ?? new List<LineItem>();
            
            // Calculate Amount for each line item (Amount = UnitPrice * Qty)
            var creditMemoLineItems = salesItemLines.Select(line =>
            {
                var unitPrice = line.SalesItemLineDetail!.UnitPrice ?? 0;
                var qty = line.SalesItemLineDetail.Qty ?? 0;
                var amount = unitPrice * qty;

                return new LineItem
                {
                    DetailType = line.DetailType,
                    // Amount is required by QuickBooks - calculate from UnitPrice * Qty
                    Amount = amount > 0 ? amount : null,
                    Description = line.Description,
                    SalesItemLineDetail = new SalesItemLineDetail
                    {
                        ItemRef = line.SalesItemLineDetail.ItemRef,
                        Qty = line.SalesItemLineDetail.Qty,
                        UnitPrice = line.SalesItemLineDetail.UnitPrice
                    }
                };
            }).ToList();

            var creditMemoRequest = new CreditMemoRequest
            {
                CustomerRef = invoice.CustomerRef,
                TxnDate = DateTime.Now.ToString("yyyy-MM-dd"),
                Line = creditMemoLineItems
            };

            // Log credit memo request details for debugging
            logger.LogInformation("Creating credit memo with {LineCount} line items for customer {CustomerId}", 
                creditMemoRequest.Line?.Count ?? 0, creditMemoRequest.CustomerRef?.Value);

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

    private static async Task<IResult> GetAllCreditNotes(
        IQuickBooksApiClient qbClient,
        IIntuitOAuthService oauthService,
        ILogger<Program> logger,
        [FromQuery] string companyId,
        [FromQuery] int? maxResults = null)
    {
        logger.LogInformation("GetAllCreditNotes called for companyId: {CompanyId}, maxResults: {MaxResults}", companyId, maxResults);
        
        // Validate company ID is provided
        if (string.IsNullOrEmpty(companyId))
            return Results.BadRequest(ApiResponse<object>.Fail("companyId is required"));

        // Retrieve stored OAuth tokens for this company, auto-refresh if expired
        var tokens = await oauthService.GetOrRefreshTokensAsync(companyId);
        if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            logger.LogWarning("GetAllCreditNotes failed: No OAuth tokens found for companyId: {CompanyId}", companyId);
            return Results.Json(
                ApiResponse<object>.Fail($"No OAuth tokens found for companyId: {companyId}. Please complete OAuth authorization first by visiting: /auth/authorize"),
                statusCode: 401);
        }

        try
        {
            // Fetch all credit memos from QuickBooks using Query API
            logger.LogInformation("Calling QuickBooks API to get all credit memos for companyId: {CompanyId}", companyId);
            var creditMemos = await qbClient.GetAllCreditMemosAsync(companyId, tokens.AccessToken, maxResults);
            
            logger.LogInformation("Retrieved {Count} credit memos for companyId: {CompanyId}", creditMemos.Count, companyId);
            return Results.Ok(ApiResponse<List<CreditMemoEntity>>.Ok(creditMemos));
        }
        catch (HttpRequestException ex)
        {
            // Handle HTTP/API errors
            logger.LogError(ex, "GetAllCreditNotes failed with HTTP error for companyId: {CompanyId}", companyId);
            return Results.BadRequest(ApiResponse<object>.Fail($"QuickBooks API error: {ex.Message}"));
        }
        catch (JsonException ex)
        {
            // Handle JSON serialization/deserialization errors
            logger.LogError(ex, "GetAllCreditNotes failed with JSON error for companyId: {CompanyId}", companyId);
            return Results.BadRequest(ApiResponse<object>.Fail($"Invalid data format: {ex.Message}"));
        }
        catch (Exception ex)
        {
            // Handle unexpected errors
            logger.LogError(ex, "GetAllCreditNotes failed with unexpected error for companyId: {CompanyId}", companyId);
            return Results.BadRequest(ApiResponse<object>.Fail($"An unexpected error occurred: {ex.Message}"));
        }
    }
}

