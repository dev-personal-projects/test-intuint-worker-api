using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using test_intuint_invoicing_api.Models;

namespace test_intuint_invoicing_api.Services;

public interface IQuickBooksApiClient
{
    Task<InvoiceEntity?> CreateInvoiceAsync(string companyId, string accessToken, InvoiceRequest request);
    Task<InvoiceEntity?> GetInvoiceAsync(string companyId, string accessToken, string invoiceId);
    Task<List<InvoiceEntity>> GetAllInvoicesAsync(string companyId, string accessToken, int? maxResults = null);
    Task<CreditMemoEntity?> CreateCreditMemoAsync(string companyId, string accessToken, CreditMemoRequest request);
    Task<CreditMemoEntity?> GetCreditMemoAsync(string companyId, string accessToken, string creditMemoId);
    Task<PaymentEntity?> CreatePaymentAsync(string companyId, string accessToken, PaymentRequest request);
    Task<CustomerEntity?> FindCustomerByNameAsync(string companyId, string accessToken, string customerName);
    Task<CustomerEntity?> CreateCustomerAsync(string companyId, string accessToken, CustomerRequest request);
    Task<InvoiceEntity?> FindDuplicateInvoiceAsync(string companyId, string accessToken, InvoiceRequest request, string customerId);
}

public class QuickBooksApiClient : IQuickBooksApiClient
{
    private readonly IntuitSettings _settings;
    private readonly HttpClient _httpClient;

    public QuickBooksApiClient(IOptions<IntuitSettings> settings, IHttpClientFactory httpClientFactory)
    {
        // Initialize QuickBooks API client with configuration and HTTP client
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<InvoiceEntity?> CreateInvoiceAsync(string companyId, string accessToken, InvoiceRequest request)
    {
        // Create a new invoice in QuickBooks Online
        // Sends POST request to QuickBooks API with invoice data
        var url = $"{_settings.BaseUrl}/v3/company/{companyId}/invoice";
        
        // Ensure Amount is calculated for all line items before serialization
        // QuickBooks requires Amount field for all SalesItemLineDetail lines
        if (request.Line != null)
        {
            foreach (var line in request.Line)
            {
                if (line.DetailType == "SalesItemLineDetail" && 
                    line.SalesItemLineDetail != null && 
                    line.SalesItemLineDetail.UnitPrice.HasValue && 
                    line.SalesItemLineDetail.Qty.HasValue)
                {
                    // Always calculate and set Amount (required by QuickBooks)
                    // This ensures Amount = UnitPrice * Qty validation passes
                    var calculatedAmount = line.SalesItemLineDetail.UnitPrice.Value * line.SalesItemLineDetail.Qty.Value;
                    line.Amount = calculatedAmount;
                }
            }
        }
        
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"QuickBooks API error: {response.StatusCode} - {content}");

        // Deserialize and return the created invoice entity
        var result = JsonSerializer.Deserialize<QuickBooksResponse<InvoiceEntity>>(content);
        return result?.Invoice;
    }

    public async Task<InvoiceEntity?> GetInvoiceAsync(string companyId, string accessToken, string invoiceId)
    {
        // Retrieve an invoice from QuickBooks by its ID
        // Returns null if invoice not found or request fails
        var url = $"{_settings.BaseUrl}/v3/company/{companyId}/invoice/{invoiceId}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return null;

        var result = JsonSerializer.Deserialize<QuickBooksResponse<InvoiceEntity>>(content);
        return result?.Invoice;
    }

    public async Task<List<InvoiceEntity>> GetAllInvoicesAsync(string companyId, string accessToken, int? maxResults = null)
    {
        // Retrieve all invoices from QuickBooks using Query API
        // Uses SQL-like query syntax to fetch all invoices, optionally limited by maxResults
        var query = "SELECT * FROM Invoice";
        if (maxResults.HasValue && maxResults.Value > 0)
        {
            query += $" MAXRESULTS {maxResults.Value}";
        }
        
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"{_settings.BaseUrl}/v3/company/{companyId}/query?query={encodedQuery}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"QuickBooks API error: {response.StatusCode} - {content}");

        // Deserialize query response and return list of invoices
        var result = JsonSerializer.Deserialize<QuickBooksResponse<InvoiceEntity>>(content);
        return result?.QueryResponse?.Invoice ?? new List<InvoiceEntity>();
    }

    public async Task<CreditMemoEntity?> CreateCreditMemoAsync(string companyId, string accessToken, CreditMemoRequest request)
    {
        // Create a credit memo (credit note) in QuickBooks Online
        // Credit memos use positive amounts - QuickBooks automatically treats them as credits
        var url = $"{_settings.BaseUrl}/v3/company/{companyId}/creditmemo";
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"QuickBooks API error: {response.StatusCode} - {content}");

        // Deserialize and return the created credit memo entity
        var result = JsonSerializer.Deserialize<QuickBooksResponse<CreditMemoEntity>>(content);
        return result?.CreditMemo;
    }

    public async Task<CreditMemoEntity?> GetCreditMemoAsync(string companyId, string accessToken, string creditMemoId)
    {
        // Retrieve a credit memo from QuickBooks by its ID
        // Returns null if credit memo not found or request fails
        var url = $"{_settings.BaseUrl}/v3/company/{companyId}/creditmemo/{creditMemoId}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return null;

        var result = JsonSerializer.Deserialize<QuickBooksResponse<CreditMemoEntity>>(content);
        return result?.CreditMemo;
    }

    public async Task<PaymentEntity?> CreatePaymentAsync(string companyId, string accessToken, PaymentRequest request)
    {
        // Create a payment in QuickBooks Online and apply it to an invoice
        // Payments reduce the invoice balance when applied
        var url = $"{_settings.BaseUrl}/v3/company/{companyId}/payment";
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"QuickBooks API error: {response.StatusCode} - {content}");

        // Deserialize and return the created payment entity
        var result = JsonSerializer.Deserialize<QuickBooksResponse<PaymentEntity>>(content);
        return result?.Payment;
    }

    public async Task<CustomerEntity?> FindCustomerByNameAsync(string companyId, string accessToken, string customerName)
    {
        // Search for a customer by name using QuickBooks Query API
        // Returns the first matching customer or null if not found
        var query = $"SELECT * FROM Customer WHERE DisplayName = '{customerName.Replace("'", "''")}'";
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"{_settings.BaseUrl}/v3/company/{companyId}/query?query={encodedQuery}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return null;

        var result = JsonSerializer.Deserialize<QuickBooksResponse<CustomerEntity>>(content);
        return result?.QueryResponse?.Customer?.FirstOrDefault();
    }

    public async Task<CustomerEntity?> CreateCustomerAsync(string companyId, string accessToken, CustomerRequest request)
    {
        // Create a new customer in QuickBooks Online
        var url = $"{_settings.BaseUrl}/v3/company/{companyId}/customer";
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"QuickBooks API error: {response.StatusCode} - {content}");

        // Deserialize and return the created customer entity
        var result = JsonSerializer.Deserialize<QuickBooksResponse<CustomerEntity>>(content);
        return result?.Customer;
    }

    public async Task<InvoiceEntity?> FindDuplicateInvoiceAsync(string companyId, string accessToken, InvoiceRequest request, string customerId)
    {
        // Check if an invoice with the same details already exists for idempotency
        // Matches by: Customer, DocNumber (if provided), and TxnDate
        var queryParts = new List<string>();
        queryParts.Add($"CustomerRef = '{customerId}'");
        
        if (!string.IsNullOrEmpty(request.DocNumber))
        {
            queryParts.Add($"DocNumber = '{request.DocNumber.Replace("'", "''")}'");
        }
        
        if (!string.IsNullOrEmpty(request.TxnDate))
        {
            queryParts.Add($"TxnDate = '{request.TxnDate}'");
        }
        
        var query = $"SELECT * FROM Invoice WHERE {string.Join(" AND ", queryParts)}";
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"{_settings.BaseUrl}/v3/company/{companyId}/query?query={encodedQuery}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await _httpClient.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return null;

        var result = JsonSerializer.Deserialize<QuickBooksResponse<InvoiceEntity>>(content);
        var invoices = result?.QueryResponse?.Invoice;
        
        if (invoices == null || invoices.Count == 0)
            return null;

        // If DocNumber was provided, return exact match
        if (!string.IsNullOrEmpty(request.DocNumber))
        {
            return invoices.FirstOrDefault(i => i.DocNumber == request.DocNumber);
        }

        // Otherwise, return the first match (most recent invoice for that customer/date)
        return invoices.OrderByDescending(i => i.TxnDate).FirstOrDefault();
    }
}

