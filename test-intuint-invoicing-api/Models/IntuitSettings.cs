namespace test_intuint_invoicing_api.Models;

// Configuration settings for Intuit OAuth and QuickBooks API
public class IntuitSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Environment { get; set; } = "sandbox";
    public string BaseUrl { get; set; } = "https://sandbox-quickbooks.api.intuit.com";
    public string AuthorizationUrl { get; set; } = "https://appcenter.intuit.com/connect/oauth2";
    public string TokenUrl { get; set; } = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";
}

// Request model for token refresh endpoint
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

