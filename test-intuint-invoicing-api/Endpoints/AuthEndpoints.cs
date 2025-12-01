using test_intuint_invoicing_api.Models;
using test_intuint_invoicing_api.Services;

namespace test_intuint_invoicing_api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/authorize", Authorize);
        app.MapGet("/auth/callback", Callback);
        app.MapPost("/auth/refresh", RefreshToken);
    }

    private static IResult Authorize(IIntuitOAuthService oauthService, string? state)
    {
        // Generate OAuth authorization URL and redirect user to Intuit login page
        var url = oauthService.GetAuthorizationUrl(state);
        return Results.Redirect(url);
    }

    private static async Task<IResult> Callback(
        IIntuitOAuthService oauthService,
        HttpContext context,
        string? code,
        string? state,
        string? realmId)
    {
        // Validate that we received both authorization code and realm ID from Intuit
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(realmId))
        {
            var errorHtml = GetErrorHtml("Missing code or realmId", "Please try the authorization flow again.");
            return Results.Content(errorHtml, "text/html");
        }

        // Exchange authorization code for access and refresh tokens
        var tokens = await oauthService.ExchangeCodeForTokensAsync(code, realmId);
        if (tokens == null)
        {
            var errorHtml = GetErrorHtml("Failed to exchange code for tokens", "Please try the authorization flow again.");
            return Results.Content(errorHtml, "text/html");
        }

        // Get the base URL (HTTP or HTTPS) for API calls
        var scheme = context.Request.Scheme;
        var host = context.Request.Host.Host;
        var port = context.Request.Host.Port ?? (scheme == "https" ? 5001 : 5000);
        var baseUrl = $"{scheme}://{host}:{port}";

        // Return user-friendly HTML page with realmId and ready-to-use curl commands
        var html = GetSuccessHtml(realmId, baseUrl, tokens);
        return Results.Content(html, "text/html");
    }

    private static string GetSuccessHtml(string realmId, string baseUrl, TokenResponse tokens)
    {
        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>OAuth Success - Intuit Invoicing API</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}
        .container {{
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            max-width: 800px;
            width: 100%;
            padding: 40px;
        }}
        .success-icon {{
            text-align: center;
            font-size: 64px;
            margin-bottom: 20px;
        }}
        h1 {{
            color: #2d3748;
            text-align: center;
            margin-bottom: 10px;
            font-size: 28px;
        }}
        .subtitle {{
            text-align: center;
            color: #718096;
            margin-bottom: 30px;
            font-size: 16px;
        }}
        .info-box {{
            background: #f7fafc;
            border-left: 4px solid #48bb78;
            padding: 20px;
            margin-bottom: 25px;
            border-radius: 4px;
        }}
        .info-box h2 {{
            color: #2d3748;
            font-size: 18px;
            margin-bottom: 15px;
        }}
        .company-id {{
            background: white;
            border: 2px solid #48bb78;
            border-radius: 6px;
            padding: 15px;
            font-family: 'Courier New', monospace;
            font-size: 20px;
            font-weight: bold;
            color: #2d3748;
            text-align: center;
            margin: 15px 0;
            word-break: break-all;
            cursor: pointer;
            position: relative;
        }}
        .company-id:hover {{
            background: #f0fff4;
        }}
        .copy-btn {{
            background: #48bb78;
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 6px;
            cursor: pointer;
            font-size: 14px;
            margin-top: 10px;
            width: 100%;
            font-weight: 600;
        }}
        .copy-btn:hover {{
            background: #38a169;
        }}
        .curl-commands {{
            background: #1a202c;
            color: #e2e8f0;
            padding: 20px;
            border-radius: 6px;
            margin-top: 20px;
            font-family: 'Courier New', monospace;
            font-size: 13px;
            overflow-x: auto;
        }}
        .curl-commands code {{
            display: block;
            white-space: pre-wrap;
            line-height: 1.6;
        }}
        .note {{
            background: #fff5e6;
            border-left: 4px solid #f6ad55;
            padding: 15px;
            margin-top: 20px;
            border-radius: 4px;
            color: #744210;
        }}
        .token-info {{
            font-size: 12px;
            color: #718096;
            margin-top: 15px;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""success-icon"">✅</div>
        <h1>OAuth Authorization Successful!</h1>
        <p class=""subtitle"">Your QuickBooks connection is now active</p>
        
        <div class=""info-box"">
            <h2>Your Company ID (Realm ID)</h2>
            <p style=""color: #718096; margin-bottom: 10px;"">Use this value as the <code>companyId</code> parameter in all API calls:</p>
            <div class=""company-id"" id=""companyId"" onclick=""copyToClipboard()"">
                {realmId}
            </div>
            <button class=""copy-btn"" onclick=""copyToClipboard()"">📋 Copy Company ID</button>
        </div>

        <div class=""curl-commands"">
            <strong style=""color: #48bb78; margin-bottom: 10px; display: block;"">Ready-to-use cURL Commands:</strong>
            <code># Health Check
curl -X GET {baseUrl}/health

# Create Invoice
curl -X POST ""{baseUrl}/api/invoices?companyId={realmId}"" \\
  -H ""Content-Type: application/json"" \\
  -d '{{
    ""CustomerRef"": {{ ""value"": ""1"" }},
    ""Line"": [{{
      ""DetailType"": ""SalesItemLineDetail"",
      ""Amount"": 100.00,
      ""Description"": ""Service fee"",
      ""SalesItemLineDetail"": {{
        ""ItemRef"": {{ ""value"": ""2"" }},
        ""Qty"": 1,
        ""UnitPrice"": 100.00
      }}
    }}],
    ""TxnDate"": ""2025-01-15"",
    ""DueDate"": ""2025-02-15""
  }}'

# Get Invoice (replace INVOICE_ID)
curl -X GET ""{baseUrl}/api/invoices/INVOICE_ID?companyId={realmId}""

# Create Credit Note (replace INVOICE_ID)
curl -X POST ""{baseUrl}/api/invoices/INVOICE_ID/credit-note?companyId={realmId}""
</code>
        </div>

        <div class=""note"">
            <strong>💡 Tip:</strong> All API endpoints require the <code>companyId={realmId}</code> query parameter. 
            Tokens are automatically stored and used for API calls.
        </div>

        <div class=""token-info"">
            Token expires in: {tokens.ExpiresIn} seconds ({tokens.ExpiresIn / 3600:F1} hours)
        </div>
    </div>

    <script>
        function copyToClipboard() {{
            const companyId = '{realmId}';
            navigator.clipboard.writeText(companyId).then(function() {{
                const btn = event.target;
                const originalText = btn.textContent;
                btn.textContent = '✅ Copied!';
                btn.style.background = '#38a169';
                setTimeout(() => {{
                    btn.textContent = originalText;
                    btn.style.background = '#48bb78';
                }}, 2000);
            }});
        }}
    </script>
</body>
</html>";
    }

    private static string GetErrorHtml(string error, string suggestion)
    {
        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>OAuth Error - Intuit Invoicing API</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #fc466b 0%, #3f5efb 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}
        .container {{
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            max-width: 600px;
            width: 100%;
            padding: 40px;
            text-align: center;
        }}
        .error-icon {{
            font-size: 64px;
            margin-bottom: 20px;
        }}
        h1 {{
            color: #e53e3e;
            margin-bottom: 10px;
        }}
        .error-message {{
            color: #2d3748;
            margin-bottom: 20px;
            font-size: 18px;
        }}
        .suggestion {{
            color: #718096;
            margin-bottom: 30px;
        }}
        a {{
            color: #667eea;
            text-decoration: none;
            font-weight: 600;
        }}
        a:hover {{
            text-decoration: underline;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""error-icon"">❌</div>
        <h1>Authorization Error</h1>
        <p class=""error-message"">{error}</p>
        <p class=""suggestion"">{suggestion}</p>
        <p><a href=""/auth/authorize"">Try Again →</a></p>
    </div>
</body>
</html>";
    }

    private static async Task<IResult> RefreshToken(
        IIntuitOAuthService oauthService,
        RefreshTokenRequest request)
    {
        // Validate refresh token is provided
        if (string.IsNullOrEmpty(request.RefreshToken))
            return Results.BadRequest(ApiResponse<object>.Fail("Refresh token is required"));

        // Exchange refresh token for new access token
        var tokens = await oauthService.RefreshTokenAsync(request.RefreshToken);
        if (tokens == null)
            return Results.BadRequest(ApiResponse<object>.Fail("Failed to refresh token"));

        return Results.Ok(ApiResponse<TokenResponse>.Ok(tokens));
    }
}

