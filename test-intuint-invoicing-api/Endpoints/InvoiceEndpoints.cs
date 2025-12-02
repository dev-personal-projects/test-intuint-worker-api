using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using test_intuint_invoicing_api.Extensions;
using test_intuint_invoicing_api.Helpers;
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

        app.MapGet("/api/invoices", GetAllInvoices)
            .WithName("GetAllInvoices")
            .WithTags("Invoices")
            .Produces<ApiResponse<InvoiceListResponse>>(200)
            .Produces<ApiResponse<object>>(401);

        app.MapGet("/api/invoices/{invoiceId}", GetInvoice)
            .WithName("GetInvoice")
            .WithTags("Invoices")
            .Produces<ApiResponse<InvoiceEntity>>(200)
            .Produces<ApiResponse<object>>(404);

        // Settle invoice using credit note + payment (hybrid approach)
        app.MapPost("/api/invoices/{invoiceId}/settle", SettleInvoice)
            .WithName("SettleInvoice")
            .WithTags("Invoices")
            .Accepts<SettlementRequest>("application/json")
            .Produces<ApiResponse<CreditMemoEntity>>(201)
            .Produces<ApiResponse<object>>(400)
            .Produces<ApiResponse<object>>(401)
            .DisableAntiforgery();
    }

    private static async Task<IResult> CreateInvoice(
        IQuickBooksApiClient qbClient,
        IIntuitOAuthService oauthService,
        [FromServices] IMemoryCache cache,
        ILogger<Program> logger,
        [FromBody] InvoiceRequest? request,
        [FromQuery] string companyId,
        [FromQuery] string? customerName = null)
    {
        logger.LogInformation("CreateInvoice called with companyId: {CompanyId}, customerName: {CustomerName}", companyId, customerName);
        
        // Validate request body
        if (request == null)
        {
            logger.LogWarning("CreateInvoice failed: Request body is null");
            return Results.BadRequest(ApiResponse<object>.Fail("Request body is required and must be valid JSON"));
        }
        
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

        try
        {
            // Handle customer name - create or find customer if name is provided
            if (!string.IsNullOrWhiteSpace(customerName))
            {
                logger.LogInformation("Customer name provided: {CustomerName}, looking up or creating customer...", customerName);
                
                // Check cache first
                var cacheKey = $"customer_{companyId}_{customerName}";
                CustomerEntity? existingCustomer = null;
                
                if (cache.TryGetValue(cacheKey, out CustomerEntity? cachedCustomer))
                {
                    logger.LogInformation("Customer found in cache: {CustomerId} - {CustomerName}", cachedCustomer?.Id, cachedCustomer?.DisplayName);
                    existingCustomer = cachedCustomer;
                }
                else
                {
                    // Try to find existing customer by name
                    existingCustomer = await qbClient.FindCustomerByNameAsync(companyId, tokens.AccessToken, customerName);
                    
                    if (existingCustomer != null)
                    {
                        // Cache the customer for 5 minutes
                        cache.Set(cacheKey, existingCustomer, TimeSpan.FromMinutes(5));
                        logger.LogInformation("Found existing customer: {CustomerId} - {CustomerName}", existingCustomer.Id, existingCustomer.DisplayName);
                    }
                }
                
                if (existingCustomer != null)
                {
                    request.CustomerRef = new Reference
                    {
                        Value = existingCustomer.Id,
                        Name = existingCustomer.DisplayName
                    };
                }
                else
                {
                    // Create new customer
                    logger.LogInformation("Customer not found, creating new customer: {CustomerName}", customerName);
                    var newCustomer = await qbClient.CreateCustomerAsync(companyId, tokens.AccessToken, new CustomerRequest
                    {
                        DisplayName = customerName,
                        CompanyName = customerName
                    });
                    
                    if (newCustomer == null)
                    {
                        logger.LogError("Failed to create customer: {CustomerName}", customerName);
                        return Results.BadRequest(ApiResponse<object>.Fail($"Failed to create customer: {customerName}"));
                    }
                    
                    // Cache the newly created customer
                    cache.Set(cacheKey, newCustomer, TimeSpan.FromMinutes(5));
                    logger.LogInformation("Customer created successfully: {CustomerId} - {CustomerName}", newCustomer.Id, newCustomer.DisplayName);
                    request.CustomerRef = new Reference
                    {
                        Value = newCustomer.Id,
                        Name = newCustomer.DisplayName
                    };
                }
            }
            
            // Validate customer reference is set
            if (request.CustomerRef == null || string.IsNullOrEmpty(request.CustomerRef.Value))
            {
                logger.LogWarning("CreateInvoice failed: CustomerRef is required");
                return Results.BadRequest(ApiResponse<object>.Fail("CustomerRef is required. Provide either CustomerRef in request body or customerName query parameter"));
            }

            // Calculate Amount for each line item if not provided (Amount = UnitPrice * Qty)
            // QuickBooks requires Amount field for all SalesItemLineDetail lines
            if (request.Line != null)
            {
                foreach (var line in request.Line)
                {
                    // Only calculate for SalesItemLineDetail lines
                    if (line.DetailType == "SalesItemLineDetail" && 
                        line.SalesItemLineDetail != null && 
                        line.SalesItemLineDetail.UnitPrice.HasValue && 
                        line.SalesItemLineDetail.Qty.HasValue)
                    {
                        // Always calculate and set Amount (required by QuickBooks)
                        // This ensures Amount = UnitPrice * Qty validation passes
                        var calculatedAmount = line.SalesItemLineDetail.UnitPrice.Value * line.SalesItemLineDetail.Qty.Value;
                        line.Amount = calculatedAmount;
                        logger.LogInformation("Calculated Amount: {Amount} = UnitPrice: {UnitPrice} × Qty: {Qty} for line: {Description}", 
                            line.Amount, line.SalesItemLineDetail.UnitPrice.Value, line.SalesItemLineDetail.Qty.Value, line.Description ?? "N/A");
                    }
                }
            }

            logger.LogInformation("Invoice request - CustomerRef: {CustomerRef}, Lines: {LineCount}", 
                request.CustomerRef.Value, request.Line?.Count ?? 0);

            // Check for duplicate invoice (idempotency check)
            logger.LogInformation("Checking for duplicate invoice with same customer, DocNumber, and TxnDate...");
            var duplicateInvoice = await qbClient.FindDuplicateInvoiceAsync(companyId, tokens.AccessToken, request, request.CustomerRef.Value);
            
            if (duplicateInvoice != null)
            {
                var duplicateCustomerName = request.CustomerRef.Name ?? "Unknown";
                var docNumber = duplicateInvoice.DocNumber ?? "N/A";
                var txnDate = duplicateInvoice.TxnDate ?? "N/A";
                
                logger.LogWarning("Duplicate invoice found. Invoice ID: {InvoiceId}, DocNumber: {DocNumber}, Customer: {CustomerName}", 
                    duplicateInvoice.Id, docNumber, duplicateCustomerName);
                
                var message = $"Invoice for customer '{duplicateCustomerName}' already exists with the same details (DocNumber: {docNumber}, Date: {txnDate}). Invoice ID: {duplicateInvoice.Id}";
                
                return Results.Json(
                    new ApiResponse<InvoiceEntity>
                    {
                        Success = true,
                        Data = duplicateInvoice,
                        Message = message
                    },
                    statusCode: 200);
            }

            // Create invoice in QuickBooks using the API client
            logger.LogInformation("No duplicate found. Calling QuickBooks API to create invoice for companyId: {CompanyId}", companyId);
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
        catch (HttpRequestException ex)
        {
            // Handle HTTP/API errors
            logger.LogError(ex, "CreateInvoice failed with HTTP error for companyId: {CompanyId}", companyId);
            return Results.BadRequest(ApiResponse<object>.Fail($"QuickBooks API error: {ex.Message}"));
        }
        catch (JsonException ex)
        {
            // Handle JSON serialization/deserialization errors
            logger.LogError(ex, "CreateInvoice failed with JSON error for companyId: {CompanyId}", companyId);
            return Results.BadRequest(ApiResponse<object>.Fail($"Invalid data format: {ex.Message}"));
        }
        catch (ArgumentNullException ex)
        {
            // Handle validation errors
            logger.LogWarning(ex, "CreateInvoice failed with validation error for companyId: {CompanyId}", companyId);
            return Results.BadRequest(ApiResponse<object>.Fail($"Validation error: {ex.Message}"));
        }
        catch (Exception ex)
        {
            // Handle unexpected errors
            logger.LogError(ex, "CreateInvoice failed with unexpected error for companyId: {CompanyId}", companyId);
            return Results.BadRequest(ApiResponse<object>.Fail($"An unexpected error occurred: {ex.Message}"));
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

    private static async Task<IResult> GetAllInvoices(
        IQuickBooksApiClient qbClient,
        IIntuitOAuthService oauthService,
        ILogger<Program> logger,
        [FromQuery] string companyId,
        [FromQuery] int? maxResults = null)
    {
        logger.LogInformation("GetAllInvoices called for companyId: {CompanyId}, maxResults: {MaxResults}", companyId, maxResults);
        
        // Validate company ID is provided
        if (string.IsNullOrEmpty(companyId))
            return Results.BadRequest(ApiResponse<object>.Fail("companyId is required"));

        // Retrieve stored OAuth tokens for this company, auto-refresh if expired
        var tokens = await oauthService.GetOrRefreshTokensAsync(companyId);
        if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            logger.LogWarning("GetAllInvoices failed: No OAuth tokens found for companyId: {CompanyId}", companyId);
            return Results.Json(
                ApiResponse<object>.Fail($"No OAuth tokens found for companyId: {companyId}. Please complete OAuth authorization first by visiting: /auth/authorize"),
                statusCode: 401);
        }

        try
        {
            // Fetch all invoices from QuickBooks using Query API
            logger.LogInformation("Calling QuickBooks API to get all invoices for companyId: {CompanyId}", companyId);
            var invoices = await qbClient.GetAllInvoicesAsync(companyId, tokens.AccessToken, maxResults);
            
            // Calculate summary statistics
            var totalAmount = invoices.Sum(i => i.TotalAmt ?? 0);
            var totalBalance = invoices.Sum(i => i.Balance ?? 0);
            var paidAmount = totalAmount - totalBalance;
            var paidCount = invoices.Count(i => (i.Balance ?? 0) == 0);
            var unpaidCount = invoices.Count - paidCount;
            
            // Create structured response with metadata
            var response = new InvoiceListResponse
            {
                Invoices = invoices,
                Count = invoices.Count,
                Summary = new InvoiceSummary
                {
                    TotalAmount = totalAmount,
                    TotalBalance = totalBalance,
                    PaidAmount = paidAmount,
                    PaidCount = paidCount,
                    UnpaidCount = unpaidCount
                }
            };
            
            logger.LogInformation("Retrieved {Count} invoices for companyId: {CompanyId}, Total Amount: {TotalAmount}, Total Balance: {TotalBalance}", 
                invoices.Count, companyId, totalAmount, totalBalance);
            
            // Return formatted response with pretty-printed JSON
            return Results.Json(ApiResponse<InvoiceListResponse>.Ok(response), 
                options: new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
        }
        catch (Exception ex)
        {
            // Return error if QuickBooks API call fails
            logger.LogError(ex, "GetAllInvoices failed with exception for companyId: {CompanyId}", companyId);
            return Results.BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Settle invoice using credit note + payment (hybrid approach)
    // This creates a credit memo and then a payment that applies the credit memo to the invoice
    private static async Task<IResult> SettleInvoice(
        IQuickBooksApiClient qbClient,
        IIntuitOAuthService oauthService,
        ILogger<Program> logger,
        string invoiceId,
        [FromBody] SettlementRequest? request,
        [FromQuery] string companyId)
    {
        logger.LogInformation("SettleInvoice called for invoiceId: {InvoiceId}, companyId: {CompanyId}", invoiceId, companyId);
        
        // Validate company ID is provided
        if (string.IsNullOrEmpty(companyId))
            return Results.BadRequest(ApiResponse<object>.Fail("companyId is required"));

        // Retrieve stored OAuth tokens for this company, auto-refresh if expired
        var tokens = await oauthService.GetOrRefreshTokensAsync(companyId);
        if (tokens == null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            logger.LogWarning("SettleInvoice failed: No OAuth tokens found for companyId: {CompanyId}", companyId);
            return Results.Json(
                ApiResponse<object>.Fail($"No OAuth tokens found for companyId: {companyId}. Please complete OAuth authorization first by visiting: /auth/authorize"),
                statusCode: 401);
        }

        try
        {
            // Fetch the invoice to get current balance and details
            logger.LogInformation("Fetching invoice {InvoiceId} to get current balance...", invoiceId);
            var invoice = await qbClient.GetInvoiceAsync(companyId, tokens.AccessToken, invoiceId);
            if (invoice == null)
            {
                logger.LogWarning("Invoice not found: {InvoiceId}", invoiceId);
                return Results.NotFound(ApiResponse<object>.Fail("Invoice not found"));
            }

            var currentBalance = invoice.Balance ?? 0;
            var invoiceTotal = invoice.TotalAmt ?? 0;
            
            // Handle case where balance is 0 but invoice has a total amount
            // This can happen if invoice was just created and balance hasn't been set yet,
            // or if invoice is already fully paid
            if (currentBalance == 0 && invoiceTotal > 0)
            {
                logger.LogWarning("Invoice balance is 0 but TotalAmt is {TotalAmt}. Invoice may already be paid or balance not yet set.", invoiceTotal);
                // If user provided an amount, use it; otherwise check if invoice is already paid
                if (request?.Amount == null || request.Amount <= 0)
                {
                    return Results.BadRequest(ApiResponse<object>.Fail($"Invoice balance is $0. The invoice may already be fully paid. Total amount: ${invoiceTotal}. If you want to settle this invoice, please specify an amount explicitly."));
                }
                // User provided an amount - allow it but use TotalAmt as the maximum
                currentBalance = invoiceTotal;
                logger.LogInformation("Using invoice TotalAmt ({TotalAmt}) as balance since Balance is 0", invoiceTotal);
            }
            
            // Determine settlement amount (default to full balance if not specified)
            var settlementAmount = request?.Amount ?? currentBalance;
            
            // Validate settlement amount
            if (settlementAmount <= 0)
            {
                logger.LogWarning("SettleInvoice failed: Invalid settlement amount");
                return Results.BadRequest(ApiResponse<object>.Fail("Settlement amount must be greater than 0"));
            }

            if (settlementAmount > currentBalance)
            {
                logger.LogWarning("SettleInvoice: Settlement amount {SettlementAmount} exceeds invoice balance {Balance}", settlementAmount, currentBalance);
                return Results.BadRequest(ApiResponse<object>.Fail($"Settlement amount (${settlementAmount}) cannot exceed invoice balance (${currentBalance})"));
            }

            logger.LogInformation("Invoice balance: {Balance}, Settlement amount: {SettlementAmount}", currentBalance, settlementAmount);

            // Calculate proportion for partial settlement
            var settlementRatio = currentBalance > 0 ? settlementAmount / currentBalance : 1m;
            var isFullSettlement = settlementAmount >= currentBalance - 0.01m;

            // Create credit memo request
            // For partial settlement, we need to proportionally adjust line items
            // Build description for credit memo
            var creditMemoDescription = request?.Description ?? 
                $"Invoice settlement - Invoice {invoice.DocNumber ?? invoiceId} - Amount: ${settlementAmount:F2}";

            // Filter invoice line items to only include SalesItemLineDetail (required for credit memo)
            // Use extension method to create adjusted line items for settlement
            var filteredLineItems = invoice.Line != null 
                ? invoice.Line.CreateAdjustedLineItemsForSettlement(settlementRatio, isFullSettlement)
                : new List<LineItem>();

            // Validate that we have valid line items for the credit memo
            // If the invoice has no SalesItemLineDetail items, we cannot create a proper credit memo
            if (filteredLineItems.Count == 0)
            {
                logger.LogWarning("SettleInvoice failed: Invoice {InvoiceId} has no SalesItemLineDetail line items. Cannot create credit memo.", invoiceId);
                return Results.BadRequest(ApiResponse<object>.Fail(
                    "Invoice has no line items that can be used for credit memo creation. " +
                    "Credit memos require SalesItemLineDetail line items. " +
                    "Please ensure the invoice has valid line items before settling."));
            }

            var creditMemoRequest = new CreditMemoRequest
            {
                CustomerRef = invoice.CustomerRef,
                TxnDate = request?.TxnDate ?? DateTime.Now.ToString("yyyy-MM-dd"),
                PrivateNote = creditMemoDescription,
                Line = filteredLineItems
            };

            // Log credit memo request details
            logger.LogInformation("Creating credit memo for settlement. Amount: {Amount}, Ratio: {Ratio}, Full: {IsFull}", 
                settlementAmount, settlementRatio, isFullSettlement);

            // Create credit memo in QuickBooks
            var creditMemo = await qbClient.CreateCreditMemoAsync(companyId, tokens.AccessToken, creditMemoRequest);
            if (creditMemo == null)
            {
                logger.LogError("Failed to create credit memo for invoice settlement: {InvoiceId}", invoiceId);
                return Results.BadRequest(ApiResponse<object>.Fail("Failed to create credit memo"));
            }

            logger.LogInformation("Credit memo created successfully. CreditMemo ID: {CreditMemoId}, Total: {TotalAmt}", 
                creditMemo.Id, creditMemo.TotalAmt);

            // Step 2: Create a payment that applies the credit memo to the invoice
            // This is the hybrid approach: credit memo + payment to actually reduce invoice balance
            // Note: QuickBooks API requires linking the payment only to the invoice.
            // QuickBooks will automatically use available credits (credit memos) for the customer
            // when applying the payment to the invoice.
            logger.LogInformation("Creating payment to apply credit memo {CreditMemoId} to invoice {InvoiceId}...", 
                creditMemo.Id, invoiceId);

            // Create payment that links to the invoice only
            // QuickBooks will automatically apply available credits (the credit memo we just created)
            // for this customer when processing the payment
            var paymentRequest = new PaymentRequest
            {
                CustomerRef = invoice.CustomerRef,
                TotalAmt = settlementAmount,
                TxnDate = request?.TxnDate ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Line = new List<PaymentLine>
                {
                    new PaymentLine
                    {
                        Amount = settlementAmount,
                        LinkedTxn = new List<LinkedTransaction>
                        {
                            // Link only to the invoice
                            // QuickBooks will automatically use the available credit memo for this customer
                            new LinkedTransaction
                            {
                                TxnId = invoiceId,
                                TxnType = "Invoice"
                            }
                        }
                    }
                }
            };

            // Create payment that applies the credit memo to the invoice
            var payment = await qbClient.CreatePaymentAsync(companyId, tokens.AccessToken, paymentRequest);
            if (payment == null)
            {
                logger.LogError("Failed to create payment to apply credit memo. Credit memo {CreditMemoId} was created but not applied to invoice {InvoiceId}. Settlement incomplete.", 
                    creditMemo.Id, invoiceId);
                
                // Return error response - settlement failed because payment creation failed
                // The credit memo exists but hasn't been applied, so the invoice balance won't be reduced
                return Results.BadRequest(ApiResponse<object>.Fail(
                    $"Settlement incomplete: Credit memo {creditMemo.Id} was created successfully, but payment creation failed. " +
                    $"The invoice balance has not been reduced. " +
                    $"Credit memo ID: {creditMemo.Id}. " +
                    $"Please check QuickBooks for details or retry the settlement."));
            }

            logger.LogInformation("Payment created successfully. Payment ID: {PaymentId}, Amount: {Amount}. " +
                "Credit memo {CreditMemoId} has been applied to invoice {InvoiceId}.", 
                payment.Id, payment.TotalAmt, creditMemo.Id, invoiceId);

            // Fetch updated invoice to verify balance reduction
            // Use retry logic with exponential backoff to wait for QuickBooks to process the payment
            var updatedInvoice = await RetryHelper.PollUntilConditionAsync(
                async () => await qbClient.GetInvoiceAsync(companyId, tokens.AccessToken, invoiceId),
                invoice => invoice != null && (invoice.Balance ?? currentBalance) != currentBalance,
                maxAttempts: 10,
                initialDelayMs: 200,
                maxDelayMs: 2000,
                onPoll: (attempt, invoice) => 
                {
                    if (invoice != null)
                    {
                        logger.LogInformation("Polling invoice balance (attempt {Attempt}). Current balance: {Balance}", 
                            attempt, invoice.Balance ?? currentBalance);
                    }
                });

            var newBalance = updatedInvoice?.Balance ?? currentBalance;
            var isSettled = newBalance <= 0.01m;

            logger.LogInformation("Invoice {InvoiceId} updated. New balance: {NewBalance}, Status: {Status}", 
                invoiceId, newBalance, isSettled ? "SETTLED" : "PARTIALLY SETTLED");

            // Return both credit memo and payment information
            // Note: We return credit memo but payment was the mechanism that actually reduced the balance
            return Results.Created($"/api/invoices/{invoiceId}/settle/{creditMemo.Id}", 
                ApiResponse<CreditMemoEntity>.Ok(creditMemo));
        }
        catch (HttpRequestException ex)
        {
            // Handle HTTP/API errors
            logger.LogError(ex, "SettleInvoice failed with HTTP error for invoiceId: {InvoiceId}", invoiceId);
            return Results.BadRequest(ApiResponse<object>.Fail($"QuickBooks API error: {ex.Message}"));
        }
        catch (JsonException ex)
        {
            // Handle JSON serialization/deserialization errors
            logger.LogError(ex, "SettleInvoice failed with JSON error for invoiceId: {InvoiceId}", invoiceId);
            return Results.BadRequest(ApiResponse<object>.Fail($"Invalid data format: {ex.Message}"));
        }
        catch (TimeoutException ex)
        {
            // Handle timeout errors (from retry logic)
            logger.LogError(ex, "SettleInvoice failed with timeout for invoiceId: {InvoiceId}", invoiceId);
            return Results.BadRequest(ApiResponse<object>.Fail($"Operation timed out: {ex.Message}"));
        }
        catch (Exception ex)
        {
            // Handle unexpected errors
            logger.LogError(ex, "SettleInvoice failed with unexpected error for invoiceId: {InvoiceId}", invoiceId);
            return Results.BadRequest(ApiResponse<object>.Fail($"An unexpected error occurred: {ex.Message}"));
        }
    }
}

