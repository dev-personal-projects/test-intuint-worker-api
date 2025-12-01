# Intuit Invoicing API

ASP.NET Core Minimal API for creating invoices and credit notes through QuickBooks Online API. This project provides a clean, organized REST API for integrating with Intuit's QuickBooks Online platform.

## Features

- OAuth 2.0 authentication with Intuit
- Create invoices programmatically
- Create credit notes to cancel/adjust invoices
- Retrieve invoice and credit note details
- Token refresh mechanism
- Swagger UI documentation

## Prerequisites

- .NET 9.0 SDK or later
- Intuit Developer account
- QuickBooks Online sandbox company (for testing)

## Project Structure

```
test-intuint-invoicing-api/
├── Endpoints/              # API endpoint handlers (controller-style)
│   ├── AuthEndpoints.cs    # OAuth authentication endpoints
│   ├── InvoiceEndpoints.cs # Invoice CRUD operations
│   ├── CreditNoteEndpoints.cs # Credit note operations
│   └── HealthEndpoints.cs  # Health check endpoint
├── Models/                 # Data models and DTOs
│   ├── ApiResponse.cs      # Standard API response wrapper
│   ├── InvoiceModels.cs    # Invoice and credit memo models
│   └── IntuitSettings.cs   # Configuration models
├── Services/                # Business logic services
│   ├── IntuitOAuthService.cs    # OAuth 2.0 flow handling
│   └── QuickBooksApiClient.cs   # QuickBooks API client
├── Program.cs              # Application entry point
├── appsettings.json        # Configuration
└── Intuit-Invoicing-API.postman_collection.json  # Postman collection for testing
```

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd test-intuint-invoicing-api/test-intuint-invoicing-api
```

### 2. Install Dependencies

```bash
dotnet restore
```

### 3. Configure Intuit Credentials

1. Register your app at [Intuit Developer Portal](https://developer.intuit.com)
2. Create a new app and note your **Client ID** and **Client Secret**
3. **Add Redirect URI in Developer Portal:**
   - Go to your app → **Settings** tab → **Redirect URIs** section
   - Make sure you're in **Development** mode (toggle at top)
   - Click **"Add URI"** button
   - **CRITICAL**: Enter exactly: `http://localhost:5000/auth/callback`
     - ✅ Use `http://` (NOT `https://`)
     - ✅ Use port `5000` (NOT `5001`)
     - ✅ Must match EXACTLY: `http://localhost:5000/auth/callback`
   - **Common mistakes to avoid:**
     - ❌ `https://localhost:5000/auth/callback` (wrong protocol - using HTTPS)
     - ❌ `http://localhost:5001/auth/callback` (wrong port)
     - ❌ `https://localhost:5001/auth/callback` (wrong protocol AND port)
   - Click **Save**
   - Wait 2-3 minutes for changes to propagate
4. Verify `appsettings.json` has the same redirect URI (should be HTTP):

```json
{
  "Intuit": {
    "ClientId": "your-client-id-here",
    "ClientSecret": "your-client-secret-here",
    "RedirectUri": "https://localhost:5001/auth/callback",
    "Environment": "sandbox",
    "BaseUrl": "https://sandbox-quickbooks.api.intuit.com"
  }
}
```

**Note:** The `appsettings.json` file already contains configured credentials. Verify they match your Intuit Developer Portal settings.

### 4. Run the Application

```bash
dotnet run
```

The API will be available at `https://localhost:5001` (or the port shown in your terminal).

### 5. Verify Configuration

Check that your configuration is correct:

```bash
# Verify the app builds successfully
dotnet build

# Check health endpoint
curl -k https://localhost:5001/health
```

### 6. Access Swagger UI

In development mode, navigate to:
- Swagger UI: `https://localhost:5001/swagger`
- OpenAPI spec: `https://localhost:5001/openapi/v1.json`

## Authentication Flow

### Initial Setup

1. **Start the application first:**
   ```bash
   cd test-intuint-invoicing-api
   dotnet run
   ```
   The app will run on `http://localhost:5000` (HTTP) and `https://localhost:5001` (HTTPS)

2. **Navigate to authorize endpoint:**
   - Open in browser: `http://localhost:5000/auth/authorize`
   - Or use: `https://localhost:5001/auth/authorize` (if HTTPS is working)

3. **Complete OAuth flow:**
   - You'll be redirected to Intuit's authorization page
   - Log in and authorize the application
   - You'll be redirected back to `/auth/callback` with `code`, `state`, and `realmId` parameters

4. **Get your Company ID:**
   - The callback will display a beautiful HTML page with your **Company ID (realmId)**
   - **SAVE THIS VALUE** - you'll need it for all API calls
   - Copy the ready-to-use curl commands shown on the page

5. Tokens are automatically stored for the `realmId` (company ID)

### Using the API

**IMPORTANT**: You must complete OAuth authorization BEFORE using the API endpoints!

All invoice and credit note endpoints require:
- `companyId` query parameter - **This is the `realmId` from the OAuth callback URL**
  - Example: `?companyId=9341455792225805`
- Valid access token (stored automatically after OAuth)

**If you get "401 Unauthorized" or "No OAuth tokens found":**
- You need to complete the OAuth flow first
- Visit: `http://localhost:5000/auth/authorize` in your browser
- Complete the authorization
- Copy the `realmId` (Company ID) from the callback page
- Then use that `realmId` as the `companyId` parameter in API calls

### Example Callback Response

After authorization, you'll see a response like:
```json
{
  "Success": true,
  "Message": "OAuth authorization successful! Use the companyId below for API calls.",
  "CompanyId": "9341455792225805",
  "RealmId": "9341455792225805",
  "Note": "Use companyId=9341455792225805 as query parameter in all API calls"
}
```

**Important**: Copy the `CompanyId` value and use it in all subsequent API calls as the `companyId` query parameter.

### Refresh Tokens

If your access token expires, use the refresh endpoint:

```bash
POST /auth/refresh
Content-Type: application/json

{
  "RefreshToken": "your-refresh-token"
}
```

## API Endpoints

### Health Check

```bash
curl -X GET http://localhost:5000/health
```

**Note:** Use `http://localhost:5000` (HTTP), not HTTPS. No `-k` flag needed for HTTP.

**Response:** `200 OK` with health status

### Authentication

#### Step 1: Authorize (Browser Required)

Open in browser:
```
https://localhost:5001/auth/authorize
```

This redirects to Intuit for authorization. After authorizing, you'll be redirected to `/auth/callback` with `code` and `realmId` parameters. Save the `realmId` (company ID) for subsequent API calls.

#### Step 2: Refresh Token

```bash
curl -X POST http://localhost:5000/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "RefreshToken": "your-refresh-token-here"
  }'
```

**Note:** Use `http://localhost:5000` (HTTP), not HTTPS.

**Response:** `200 OK` with new access and refresh tokens

### Invoices

#### Create Invoice

```bash
curl -X POST "http://localhost:5000/api/invoices?companyId=YOUR_REALM_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "CustomerRef": {
      "value": "1"
    },
    "Line": [
      {
        "DetailType": "SalesItemLineDetail",
        "Amount": 100.00,
        "Description": "Service fee",
        "SalesItemLineDetail": {
          "ItemRef": {
            "value": "2"
          },
          "Qty": 1,
          "UnitPrice": 100.00
        }
      }
    ],
    "TxnDate": "2025-01-15",
    "DueDate": "2025-02-15"
  }'
```

**Note:** Use `http://localhost:5000` (HTTP), not `https://localhost:5000` (HTTPS). The app runs on HTTP port 5000.

**Note:** Replace `YOUR_REALM_ID` with the `realmId` from the OAuth callback.

**Response:** `201 Created` with invoice details including the invoice ID.

#### Get Invoice

```bash
curl -X GET "http://localhost:5000/api/invoices/INVOICE_ID?companyId=YOUR_REALM_ID"
```

**Note:** Use `http://localhost:5000` (HTTP), not HTTPS.

**Note:** Replace `INVOICE_ID` with the actual invoice ID and `YOUR_REALM_ID` with your company ID.

**Response:** `200 OK` with invoice details

### Credit Notes

#### Create Credit Note (Cancel Invoice)

```bash
curl -X POST "http://localhost:5000/api/invoices/INVOICE_ID/credit-note?companyId=YOUR_REALM_ID"
```

**Note:** Use `http://localhost:5000` (HTTP), not HTTPS.

**Note:** 
- Replace `INVOICE_ID` with the invoice ID you want to cancel
- Replace `YOUR_REALM_ID` with your company ID
- This creates a credit memo matching the invoice amount, effectively canceling it

**Response:** `201 Created` with credit memo details including the credit note ID

#### Get Credit Note

```bash
curl -X GET "http://localhost:5000/api/credit-notes/CREDIT_NOTE_ID?companyId=YOUR_REALM_ID"
```

**Note:** Use `http://localhost:5000` (HTTP), not HTTPS.

**Note:** Replace `CREDIT_NOTE_ID` with the actual credit note ID and `YOUR_REALM_ID` with your company ID.

**Response:** `200 OK` with credit memo details

## Testing

### Option 1: Using Postman (Recommended)

Postman is easier for testing complex JSON requests. A comprehensive Postman collection is included in the project with proper payloads, examples, and automated scripts.

1. **Import the Collection:**
   - Open Postman
   - Click **Import** → Select `Intuit-Invoicing-API.postman_collection.json`
   - The collection will be imported with all endpoints pre-configured

2. **Set Environment Variables:**
   - After OAuth, copy your `realmId` (Company ID) from the callback URL
   - In Postman, click on the collection → **Variables** tab
   - Set `companyId` variable to your `realmId` (e.g., `9341455793300229`)
   - The `baseUrl` is already set to `http://localhost:5000`
   - All requests will automatically use these variables

3. **Test Endpoints:**
   - **Health Check**: Just click Send to verify API is running
   - **OAuth - Authorize**: Open the URL in browser (not Postman) to complete OAuth flow
   - **Create Invoice - Simple**: Pre-configured with proper payload, automatically sets dates
   - **Create Invoice - Multiple Line Items**: Example with 3 line items
   - **Create Invoice - With Custom DocNumber**: Example with custom invoice number
   - **Get Invoice**: Uses `{{invoiceId}}` variable (automatically set after creating invoice)
   - **Create Credit Note**: Uses `{{invoiceId}}` to cancel an invoice
   - **Get Credit Note**: Uses `{{creditNoteId}}` variable (automatically set after creating credit note)

**Features of the Postman Collection:**
- ✅ **Proper payloads** matching exact API model structure
- ✅ **Multiple examples** (simple, multiple items, custom doc number)
- ✅ **Automated scripts** that set dynamic dates (today, due date)
- ✅ **Auto-save IDs** - invoiceId and creditNoteId are automatically saved after creation
- ✅ **Test assertions** - validates responses automatically
- ✅ **Detailed descriptions** - explains each field and parameter
- ✅ **Variable management** - easy to update companyId and reuse invoice IDs
- ✅ **Syntax highlighting** - easy JSON editing
- ✅ **Response formatting** - formatted JSON responses

### Option 2: Using cURL

## Testing with cURL

### Quick Start Testing Flow

1. **Start the application:**
   ```bash
   cd test-intuint-invoicing-api
   dotnet run
   ```

2. **Check health:**
   ```bash
   curl http://localhost:5000/health
   ```

3. **Authorize (use browser):**
   - Open: `http://localhost:5000/auth/authorize`
   - Complete OAuth flow
   - After authorization, you'll be redirected to callback URL like:
     `https://localhost:5001/auth/callback?code=...&state=...&realmId=9341455792225805`
   - **Copy the `realmId` value** (e.g., `9341455792225805`) - this is your `companyId`
   - The callback page will also display the CompanyId clearly

4. **Create an invoice (replace YOUR_REALM_ID with the realmId from step 3):**
   
   **Single-line command (recommended):**
   ```bash
   curl -X POST "http://localhost:5000/api/invoices?companyId=YOUR_REALM_ID" -H "Content-Type: application/json" -d '{"CustomerRef":{"value":"1"},"Line":[{"DetailType":"SalesItemLineDetail","Amount":100.00,"Description":"Test Invoice","SalesItemLineDetail":{"ItemRef":{"value":"2"},"Qty":1,"UnitPrice":100.00}}],"TxnDate":"2025-01-15","DueDate":"2025-02-15"}'
   ```
   
   **Multi-line command (if you prefer):**
   ```bash
   curl -X POST "http://localhost:5000/api/invoices?companyId=YOUR_REALM_ID" \
     -H "Content-Type: application/json" \
     -d '{"CustomerRef":{"value":"1"},"Line":[{"DetailType":"SalesItemLineDetail","Amount":100.00,"Description":"Test Invoice","SalesItemLineDetail":{"ItemRef":{"value":"2"},"Qty":1,"UnitPrice":100.00}}],"TxnDate":"2025-01-15","DueDate":"2025-02-15"}'
   ```
   
   **Note:** Make sure there are NO spaces after the backslash (`\`) in multi-line commands!

5. **Get the created invoice (replace INVOICE_ID with actual invoice ID):**
   ```bash
   curl -X GET "http://localhost:5000/api/invoices/INVOICE_ID?companyId=YOUR_REALM_ID" | jq
   ```

6. **Create credit note (cancel invoice):**
   ```bash
   curl -X POST "http://localhost:5000/api/invoices/INVOICE_ID/credit-note?companyId=YOUR_REALM_ID" | jq
   ```

### Tips for Testing

- Use `http://localhost:5000` (HTTP), not `https://` (HTTPS) - the app runs on HTTP port 5000
- Pipe to `jq` for formatted JSON output: `curl ... | jq`
- Save responses to variables in bash:
  ```bash
  INVOICE_RESPONSE=$(curl -X POST "https://localhost:5001/api/invoices?companyId=YOUR_REALM_ID" \
    -H "Content-Type: application/json" \
    -d '{...}' \
    -k)
  
  INVOICE_ID=$(echo $INVOICE_RESPONSE | jq -r '.data.id')
  ```

- View full response with headers:
  ```bash
  curl -v -X GET "http://localhost:5000/api/invoices/INVOICE_ID?companyId=YOUR_REALM_ID"
  ```

## Development

### Building the Project

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Code Structure

- **Endpoints**: Each endpoint group is in its own file under `Endpoints/`
- **Services**: Business logic is separated into service classes
- **Models**: DTOs and data models are in the `Models/` folder
- **Comments**: Functions include inline comments explaining their purpose

### Adding New Endpoints

1. Create or update the appropriate endpoint file in `Endpoints/`
2. Add the endpoint method with proper comments
3. Register the endpoint mapping in `Program.cs`

Example:
```csharp
public static void MapMyEndpoints(this WebApplication app)
{
    app.MapGet("/api/my-endpoint", MyEndpoint);
}

private static IResult MyEndpoint()
{
    // Your logic here
    return Results.Ok();
}
```

Then in `Program.cs`:
```csharp
app.MapMyEndpoints();
```

## Architecture

### OAuth Flow

1. User initiates OAuth via `/auth/authorize`
2. Redirected to Intuit for authorization
3. Intuit redirects back with authorization code
4. Code is exchanged for access/refresh tokens
5. Tokens are stored in-memory (keyed by `realmId`)

### API Request Flow

1. Client sends request with `companyId` query parameter
2. System retrieves stored tokens for that company
3. Request is forwarded to QuickBooks API with Bearer token
4. Response is returned to client

### Credit Note Creation

When creating a credit note:
1. Original invoice is fetched from QuickBooks
2. Credit memo is created with matching line items (negative amounts)
3. Credit memo effectively cancels the invoice

## Troubleshooting

### Common Issues

**Issue: "ERR_CONNECTION_REFUSED" or "This site can't be reached"**
- **Error**: Browser shows connection refused when accessing callback URL
- **Solution**: 
  1. **Check if app is running**: Make sure `dotnet run` is executing
  2. **Use HTTP instead of HTTPS**: If HTTPS port 5001 doesn't work, use HTTP port 5000:
     - Update `appsettings.json`: Change `RedirectUri` to `http://localhost:5000/auth/callback`
     - Add `http://localhost:5000/auth/callback` in Intuit Developer Portal → Settings → Redirect URIs
     - Use `http://localhost:5000/auth/authorize` to start OAuth flow
  3. **Or fix HTTPS**: Ensure .NET development certificate is installed:
     ```bash
     dotnet dev-certs https --trust
     ```

**Issue: "The redirect_uri query parameter value is invalid"**
- **Error Message**: "Make sure it is listed in the Redirect URIs section on your app's keys tab and matches it exactly"
- **Solution**: 
  1. Go to [Intuit Developer Portal](https://developer.intuit.com/app/developer/myapps)
  2. Select your app (Client ID: `ABNlWkUkrhvReqHfo4yfXCgxn6FFdRnfbdfXE3HfaMgjD5WPep`)
  3. Navigate to **Settings** tab → **Redirect URIs** section
  4. Make sure you're in **Development** mode (not Production)
  5. Click **"Add URI"** button
  6. Add **BOTH** URIs (you can add multiple):
     - `https://localhost:5001/auth/callback` (for HTTPS)
     - `http://localhost:5000/auth/callback` (for HTTP - backup option)
  7. **Important**: 
     - The URI must match EXACTLY (including protocol, no trailing slash)
     - Do NOT use the OAuth2 Playground URL - that's only for manual testing
  8. Click **Save**
  9. Wait 2-3 minutes for changes to propagate
  10. Try the OAuth flow again

**Issue: "Unauthorized" responses**
- **Solution**: Ensure you've completed the OAuth flow and tokens are stored for the `companyId`

**Issue: "Missing code or realmId"**
- **Solution**: Complete the OAuth authorization flow from `/auth/authorize`

**Issue: "Failed to create invoice"**
- **Solution**: 
  - Verify customer and item IDs exist in QuickBooks
  - Check that required fields are provided
  - Ensure access token is valid

**Issue: Port already in use**
- **Solution**: Change the port in `Properties/launchSettings.json` or use:
  ```bash
  dotnet run --urls "https://localhost:5002"
  ```
  - **Note**: If you change the port, also update the `RedirectUri` in `appsettings.json` and in Intuit Developer Portal

### Debugging

- Check application logs in the console
- Use Swagger UI to test endpoints interactively
- Verify `appsettings.json` has correct Intuit credentials
- Ensure QuickBooks sandbox company is set up correctly

## Configuration

### Environment Variables

You can override configuration using environment variables:

```bash
export Intuit__ClientId="your-client-id"
export Intuit__ClientSecret="your-client-secret"
```

### Production Settings

For production:
- Update `BaseUrl` to production URL
- Change `Environment` to `production`
- Use secure token storage (current implementation uses in-memory storage)
- Configure proper HTTPS certificates

## Contributing

1. Create a feature branch
2. Make your changes with clear comments
3. Ensure code builds without errors
4. Test your changes
5. Submit a pull request

### Code Style

- Keep functions focused and single-purpose
- Add comments explaining what functions do
- Use meaningful variable names
- Follow existing code structure

## Resources

- [Intuit Developer Portal](https://developer.intuit.com)
- [QuickBooks Online API Documentation](https://developer.intuit.com/app/developer/qbo/docs)
- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)

## License

See LICENSE file for details.
