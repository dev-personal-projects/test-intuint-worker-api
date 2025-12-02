using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using test_intuint_invoicing_api.Models;

namespace test_intuint_invoicing_api.Services;

public interface IIntuitOAuthService
{
    string GetAuthorizationUrl(string? state = null);
    Task<TokenResponse?> ExchangeCodeForTokensAsync(string code, string realmId);
    Task<TokenResponse?> RefreshTokenAsync(string refreshToken);
    void StoreTokens(string realmId, TokenResponse tokens);
    TokenResponse? GetTokens(string realmId);
    Task<TokenResponse?> GetOrRefreshTokensAsync(string realmId);
}

public class IntuitOAuthService : IIntuitOAuthService
{
    private readonly IntuitSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, TokenResponse> _tokenStore = new();
    private readonly string _tokensFilePath;
    private readonly object _lockObject = new();

    public IntuitOAuthService(IOptions<IntuitSettings> settings, IHttpClientFactory httpClientFactory)
    {
        // Initialize OAuth service with configuration and HTTP client
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient();
        
        // Set up persistent token storage file path
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IntuitInvoicingAPI");
        Directory.CreateDirectory(appDataPath);
        _tokensFilePath = Path.Combine(appDataPath, "tokens.json");
        
        // Load tokens from file on startup
        LoadTokensFromFile();
    }

    public string GetAuthorizationUrl(string? state = null)
    {
        // Generate OAuth 2.0 authorization URL for redirecting user to Intuit login
        // Creates a unique state parameter if not provided for CSRF protection
        state ??= Guid.NewGuid().ToString();
        var scopes = "com.intuit.quickbooks.accounting";
        var redirectUri = Uri.EscapeDataString(_settings.RedirectUri);
        
        return $"{_settings.AuthorizationUrl}?client_id={_settings.ClientId}&scope={scopes}&redirect_uri={redirectUri}&response_type=code&state={state}";
    }

    public async Task<TokenResponse?> ExchangeCodeForTokensAsync(string code, string realmId)
    {
        // Exchange authorization code for access and refresh tokens
        // Uses Basic authentication with client credentials
        var request = new HttpRequestMessage(HttpMethod.Post, _settings.TokenUrl);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
        
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", _settings.RedirectUri)
        });

        // OAuth token exchange - use retry for transient failures
        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return null;

        // Deserialize token response and store tokens for future API calls
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content);
        if (tokenResponse != null)
        {
            // Set expiration time
            tokenResponse.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            StoreTokens(realmId, tokenResponse);
        }

        return tokenResponse;
    }

    public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken)
    {
        // Refresh an expired access token using the refresh token
        // Returns new access and refresh tokens
        var request = new HttpRequestMessage(HttpMethod.Post, _settings.TokenUrl);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
        
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        });

        // OAuth token exchange - use retry for transient failures
        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return null;

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content);
        if (tokenResponse != null)
        {
            // Set expiration time
            tokenResponse.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
        }
        
        return tokenResponse;
    }

    public void StoreTokens(string realmId, TokenResponse tokens)
    {
        // Store OAuth tokens in memory and persist to file, keyed by company ID (realmId)
        Dictionary<string, TokenResponse> tokenStoreSnapshot;
        lock (_lockObject)
        {
            _tokenStore[realmId] = tokens;
            // Create a snapshot of the token store while lock is held to avoid race conditions
            tokenStoreSnapshot = new Dictionary<string, TokenResponse>(_tokenStore);
        }
        
        // Fire and forget async file save to avoid blocking
        // Use snapshot to prevent race conditions with concurrent modifications
        _ = Task.Run(async () => await SaveTokensToFileAsync(tokenStoreSnapshot));
    }

    public TokenResponse? GetTokens(string realmId)
    {
        // Retrieve stored OAuth tokens for a specific company
        // Returns null if no tokens found for the given realmId
        lock (_lockObject)
        {
            return _tokenStore.TryGetValue(realmId, out var tokens) ? tokens : null;
        }
    }

    public async Task<TokenResponse?> GetOrRefreshTokensAsync(string realmId)
    {
        // Get tokens for a company, automatically refresh if expired
        var tokens = GetTokens(realmId);
        
        if (tokens == null)
            return null;
        
        // Check if token is expired (with 5 minute buffer)
        // Note: Intuit tokens typically expire in 1 hour (3600 seconds)
        // We'll refresh if less than 5 minutes remaining
        var expiresAt = tokens.ExpiresAt;
        if (expiresAt.HasValue && expiresAt.Value <= DateTime.UtcNow.AddMinutes(5))
        {
            // Token is expired or about to expire, try to refresh
            if (!string.IsNullOrEmpty(tokens.RefreshToken))
            {
                var refreshedTokens = await RefreshTokenAsync(tokens.RefreshToken);
                if (refreshedTokens != null)
                {
                    // Update with new tokens, preserving realmId
                    refreshedTokens.ExpiresAt = DateTime.UtcNow.AddSeconds(refreshedTokens.ExpiresIn);
                    StoreTokens(realmId, refreshedTokens);
                    return refreshedTokens;
                }
            }
            // Refresh failed, return null to trigger re-authorization
            return null;
        }
        
        return tokens;
    }

    private void LoadTokensFromFile()
    {
        // Load tokens from persistent storage file
        if (!File.Exists(_tokensFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_tokensFilePath);
            var tokenData = JsonSerializer.Deserialize<Dictionary<string, TokenResponse>>(json);
            if (tokenData != null)
            {
                lock (_lockObject)
                {
                    foreach (var kvp in tokenData)
                    {
                        _tokenStore[kvp.Key] = kvp.Value;
                    }
                }
            }
        }
        catch
        {
            // If file is corrupted or unreadable, start fresh
            // This is acceptable for development
        }
    }

    private async Task SaveTokensToFileAsync(Dictionary<string, TokenResponse> tokenStoreSnapshot)
    {
        // Save tokens to persistent storage file asynchronously
        // Uses a snapshot of the token store to avoid race conditions
        try
        {
            var json = JsonSerializer.Serialize(tokenStoreSnapshot, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            await File.WriteAllTextAsync(_tokensFilePath, json);
        }
        catch (Exception)
        {
            // If file write fails, continue with in-memory storage only
            // This is acceptable for development
            // Note: We can't use ILogger here as this is a fire-and-forget operation
            // In production, consider using a proper logging mechanism
        }
    }
}

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    // Calculated expiration time (not from API, set when token is received)
    [JsonIgnore]
    public DateTime? ExpiresAt { get; set; }
}

