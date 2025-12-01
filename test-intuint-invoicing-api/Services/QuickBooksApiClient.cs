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
    Task<CreditMemoEntity?> CreateCreditMemoAsync(string companyId, string accessToken, CreditMemoRequest request);
    Task<CreditMemoEntity?> GetCreditMemoAsync(string companyId, string accessToken, string creditMemoId);
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

    public async Task<CreditMemoEntity?> CreateCreditMemoAsync(string companyId, string accessToken, CreditMemoRequest request)
    {
        // Create a credit memo (credit note) in QuickBooks Online
        // Used to cancel or adjust invoices by creating negative amounts
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
}

