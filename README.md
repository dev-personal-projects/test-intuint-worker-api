# Intuit Invoicing API

ASP.NET Core Minimal API for creating invoices and credit notes through QuickBooks Online API. This project provides a clean, organized REST API for integrating with Intuit's QuickBooks Online platform.

## 🚀 Quick Start for New Developers

1. **Clone and navigate:**
   ```bash
   git clone <repository-url>
   cd test-intuint-invoicing-api/test-intuint-invoicing-api
   ```

2. **Set up configuration:**
   ```bash
   cp appsettings.json.example appsettings.json
   # Edit appsettings.json with your Intuit credentials (see below)
   ```

3. **Install and run:**
   ```bash
   dotnet restore
   dotnet run
   ```

4. **Get your Intuit credentials:**
   - Register at [Intuit Developer Portal](https://developer.intuit.com)
   - Create an app and get **Client ID** & **Client Secret**
   - Add redirect URI: `http://localhost:5000/auth/callback` in Developer Portal

5. **Authorize and get Company ID:**
   - Visit: `http://localhost:5000/auth/authorize` in your browser
   - Complete OAuth flow
   - Copy the `realmId` (Company ID) from the callback page

6. **Start testing:**
   ```bash
   # List all invoices
   ./scripts/list-invoices.sh YOUR_REALM_ID
   
   # Create an invoice
   ./scripts/create-invoice.sh "Customer Name" YOUR_REALM_ID
   ```

📖 **For detailed setup instructions, see [Getting Started](#getting-started) section below.**

## Features

- OAuth 2.0 authentication with Intuit
- Create invoices programmatically with automatic customer creation
- Create credit notes to cancel/adjust invoices
- List all invoices and credit notes with summary statistics
- Retrieve invoice and credit note details by ID
- Apply payments to invoices and automatically create credit notes
- Token refresh mechanism with persistent storage
- Idempotency checks to prevent duplicate invoices
- Swagger UI documentation
- Comprehensive shell scripts for easy testing

## Prerequisites

- .NET 9.0 SDK or later
- Intuit Developer account
- QuickBooks Online sandbox company (for testing)
- `jq` (optional but recommended) - For better JSON formatting in shell scripts
  - Install on Linux: `apt-get update && apt-get install -y jq`
  - Install on macOS: `brew install jq`
  - Install on Windows: Download from [jq official site](https://stedolan.github.io/jq/download/)
- `jq` (optional but recommended) - For better JSON formatting in shell scripts
  - Install on Linux: `apt-get update && apt-get install -y jq`
  - Install on macOS: `brew install jq`
  - Install on Windows: Download from [jq official site](https://stedolan.github.io/jq/download/)

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
├── appsettings.json        # Configuration (create from appsettings.json.example)
├── appsettings.json.example # Configuration template (copy to appsettings.json)
└── scripts/                # Shell scripts for testing
    ├── create-invoice.sh
    ├── list-invoices.sh
    ├── get-invoice.sh
    ├── settle-invoice.sh
    ├── create-credit-note.sh
    ├── list-credit-notes.sh
    └── get-credit-note.sh
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

#### Step 1: Get Intuit Developer Credentials

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

#### Step 2: Create Configuration File

1. **Copy the example configuration file:**
   ```bash
   cd test-intuint-invoicing-api
   cp appsettings.json.example appsettings.json
   ```

2. **Edit `appsettings.json` and add your credentials:**
   ```json
   {
     "Intuit": {
       "ClientId": "YOUR_CLIENT_ID_HERE",
       "ClientSecret": "YOUR_CLIENT_SECRET_HERE",
       "RedirectUri": "http://localhost:5000/auth/callback",
       "Environment": "sandbox",
       "BaseUrl": "https://sandbox-quickbooks.api.intuit.com",
       "AuthorizationUrl": "https://appcenter.intuit.com/connect/oauth2",
       "TokenUrl": "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer"
     }
   }
   ```

3. **Replace the placeholders:**
   - Replace `YOUR_CLIENT_ID_HERE` with your actual Client ID from Intuit Developer Portal
   - Replace `YOUR_CLIENT_SECRET_HERE` with your actual Client Secret
   - Verify `RedirectUri` matches exactly what you added in the Developer Portal

**Important Security Note:**
- ⚠️ **Never commit `appsettings.json`** to version control (it's already in `.gitignore`)
- ✅ The `appsettings.json.example` file is safe to commit (contains no secrets)
- ✅ Each developer should create their own `appsettings.json` from the example

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

#### Get All Credit Notes

**Endpoint:** `GET /api/credit-notes`

```bash
curl -X GET "http://localhost:5000/api/credit-notes?companyId=YOUR_REALM_ID"
```

**Optional Parameters:**
- `maxResults` - Limit the number of results (e.g., `?companyId=YOUR_REALM_ID&maxResults=50`)

**Response:** `200 OK` with a list of all credit memos

**Note:** This endpoint retrieves all credit notes (credit memos) created in QuickBooks for the specified company.

#### Create Credit Note (Cancel Invoice)

**Endpoint:** `POST /api/invoices/{invoiceId}/credit-note`

```bash
curl -X POST "http://localhost:5000/api/invoices/INVOICE_ID/credit-note?companyId=YOUR_REALM_ID"
```

**Note:** Use `http://localhost:5000` (HTTP), not HTTPS.

**Note:** 
- Replace `INVOICE_ID` with the invoice ID you want to cancel
- Replace `YOUR_REALM_ID` with your company ID
- This creates a credit memo matching the invoice amount, effectively canceling it
- Can be created after invoice settlement (regardless of payment amount)

**Response:** `201 Created` with credit memo details including the credit note ID

#### Get Credit Note

**Endpoint:** `GET /api/credit-notes/{creditNoteId}`

```bash
curl -X GET "http://localhost:5000/api/credit-notes/CREDIT_NOTE_ID?companyId=YOUR_REALM_ID"
```

**Note:** Use `http://localhost:5000` (HTTP), not HTTPS.

**Note:** Replace `CREDIT_NOTE_ID` with the actual credit note ID and `YOUR_REALM_ID` with your company ID.

**Response:** `200 OK` with credit memo details

#### Viewing Credit Notes in QuickBooks Portal

Credit notes (credit memos) can be viewed in QuickBooks Online through:

1. **Sales Menu → Products and Services**
   - Click on the "Credit Memos" tab to see all credit memos

2. **Sales → All Sales**
   - Filter by "Credit Memos" to view credit notes

3. **Reports → Sales**
   - Various sales reports include credit memo information

**Note:** Credit memos appear with negative amounts in QuickBooks, but when creating them via the API, you provide positive amounts (QuickBooks automatically treats them as credits).

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

### Option 3: Using Shell Scripts

The project includes convenient shell scripts in the `scripts/` folder for common operations. All scripts support optional `jq` for enhanced JSON formatting.

**Note:** Scripts work without `jq`, but installing it provides better formatted output. See [Prerequisites](#prerequisites) for installation instructions.

#### List All Invoices

```bash
./scripts/list-invoices.sh [companyId] [maxResults]
```

**Examples:**
```bash
# List all invoices (uses default company ID)
./scripts/list-invoices.sh

# List invoices for specific company
./scripts/list-invoices.sh 9341455793300229

# List invoices with max results limit
./scripts/list-invoices.sh 9341455793300229 50
```

**Features:**
- Displays summary with total invoice count
- Pretty-prints JSON response (if `jq` is installed)
- Shows invoice IDs, DocNumbers, totals, and balances
- Provides quick action commands

#### List All Credit Notes

```bash
./scripts/list-credit-notes.sh [companyId] [maxResults]
```

**Examples:**
```bash
# List all credit notes (uses default company ID)
./scripts/list-credit-notes.sh

# List credit notes for specific company
./scripts/list-credit-notes.sh 9341455793300229

# List credit notes with max results limit
./scripts/list-credit-notes.sh 9341455793300229 50
```

**Features:**
- Displays summary with total credit note count
- Pretty-prints JSON response (if `jq` is installed)
- Shows credit note IDs, DocNumbers, totals, and customer info
- Provides quick action commands and portal location info

#### Get Invoice by ID

```bash
./scripts/get-invoice.sh <invoiceId> [companyId]
```

**Examples:**
```bash
# Get invoice by ID (uses default company ID)
./scripts/get-invoice.sh 147

# Get invoice for specific company
./scripts/get-invoice.sh 147 9341455793300229
```

**Features:**
- Displays detailed invoice summary (ID, DocNumber, Customer, Dates, Amounts, Status)
- Shows all line items with quantities and prices
- Pretty-prints full JSON response (if `jq` is installed)
- Provides quick action commands (list, settle, create credit note)
- Shows invoice status (PAID/UNPAID)

#### Get Credit Note by ID

```bash
./scripts/get-credit-note.sh <creditNoteId> [companyId]
```

**Examples:**
```bash
# Get credit note by ID (uses default company ID)
./scripts/get-credit-note.sh 148

# Get credit note for specific company
./scripts/get-credit-note.sh 148 9341455793300229
```

**Features:**
- Displays detailed credit note summary (ID, DocNumber, Customer, Date, Amount)
- Shows all line items with quantities and prices
- Pretty-prints full JSON response (if `jq` is installed)
- Provides quick action commands and portal location info
- Handles negative amounts correctly (displays as positive credit)

#### Create Invoice

```bash
./scripts/create-invoice.sh [customerName] [companyId]
```

**Example:**
```bash
./scripts/create-invoice.sh "Shipht It Company" 9341455793300229
```

**Features:**
- Automatically creates customer if it doesn't exist
- Uses today's date and calculates due date (30 days)
- Comprehensive invoice with multiple line items
- Idempotency check to prevent duplicates

#### Settle Invoice

```bash
./scripts/settle-invoice.sh <invoiceId> <paymentAmount> [companyId]
```

**Example:**
```bash
./scripts/settle-invoice.sh 147 16567.00 9341455793300229
```

**Features:**
- Applies payment to invoice
- Verifies updated balance
- Automatically creates credit note after settlement
- Shows settlement status (fully paid or partially paid)

#### Create Credit Note

```bash
./scripts/create-credit-note.sh <invoiceId> [companyId]
```

**Example:**
```bash
./scripts/create-credit-note.sh 147 9341455793300229
```

**Features:**
- Creates credit memo matching invoice
- Verifies invoice settlement status
- Shows updated invoice balance

### Tips for Testing

- Use `http://localhost:5000` (HTTP), not `https://` (HTTPS) - the app runs on HTTP port 5000
- Pipe to `jq` for formatted JSON output: `curl ... | jq`
- Install `jq` for better script output:
  - **Linux**: `apt-get update && apt-get install -y jq` (or `sudo apt-get install jq` if not root)
  - **macOS**: `brew install jq`
  - **Windows**: Download from [jq official site](https://stedolan.github.io/jq/download/)
- All shell scripts automatically detect and use `jq` if installed for enhanced formatting
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
2. Credit memo is created with matching line items (positive amounts - QuickBooks treats them as credits automatically)
3. Credit memo effectively cancels the invoice
4. Credit notes can be created after invoice settlement (regardless of payment amount)

**Note:** The `settle-invoice.sh` script automatically creates a credit note after applying a payment to an invoice, regardless of whether the invoice is fully or partially paid.

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

We welcome contributions! Here's how to get started:

### Setting Up for Development

1. **Fork and clone the repository:**
   ```bash
   git clone <your-fork-url>
   cd test-intuint-invoicing-api/test-intuint-invoicing-api
   ```

2. **Set up your development environment:**
   ```bash
   # Install dependencies
   dotnet restore
   
   # Create your configuration file from the example
   cp appsettings.json.example appsettings.json
   
   # Edit appsettings.json with your Intuit credentials
   # Replace YOUR_CLIENT_ID_HERE and YOUR_CLIENT_SECRET_HERE
   # (See "Configure Intuit Credentials" section above for detailed steps)
   ```

3. **Get your Intuit Developer credentials:**
   - If you don't have an Intuit Developer account, create one at [Intuit Developer Portal](https://developer.intuit.com)
   - Create a new app in the Developer Portal
   - Copy your **Client ID** and **Client Secret**
   - Add the redirect URI: `http://localhost:5000/auth/callback` in your app settings

4. **Verify everything works:**
   ```bash
   # Build the project
   dotnet build
   
   # Run the application
   dotnet run
   
   # Test the health endpoint
   curl http://localhost:5000/health
   ```

### Development Workflow

1. **Create a feature branch:**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes:**
   - Follow the existing code structure
   - Add comments explaining what functions do
   - Keep functions focused and single-purpose
   - Use meaningful variable names

3. **Test your changes:**
   ```bash
   # Build to check for errors
   dotnet build
   
   # Run the application and test endpoints
   dotnet run
   
   # Use the provided shell scripts to test functionality
   ./scripts/list-invoices.sh
   ```

4. **Commit your changes:**
   ```bash
   git add .
   git commit -m "Description of your changes"
   ```

5. **Push and create a pull request:**
   ```bash
   git push origin feature/your-feature-name
   ```
   Then create a pull request on GitHub.

### Code Style Guidelines

- **Keep functions focused and single-purpose** - Each function should do one thing well
- **Add comments explaining what functions do** - Help other developers understand your code
- **Use meaningful variable names** - Make code self-documenting
- **Follow existing code structure** - Maintain consistency with the project
- **Handle errors gracefully** - Provide clear error messages
- **Test your changes** - Use the provided scripts or Swagger UI

### Project Structure Guidelines

- **Endpoints** go in `Endpoints/` folder - One file per endpoint group
- **Services** go in `Services/` folder - Business logic and external API clients
- **Models** go in `Models/` folder - DTOs and data models
- **Scripts** go in `scripts/` folder - Shell scripts for testing and automation

### Before Submitting

- ✅ Code builds without errors (`dotnet build`)
- ✅ All endpoints tested and working
- ✅ Comments added for new functions
- ✅ No hardcoded credentials or secrets
- ✅ Follows existing code patterns
- ✅ README updated if adding new features
- ✅ `appsettings.json` is NOT committed (only `appsettings.json.example`)

### Common Issues for Contributors

**Issue: "No OAuth tokens found"**
- Solution: Complete the OAuth flow first by visiting `http://localhost:5000/auth/authorize`

**Issue: "Invalid redirect_uri"**
- Solution: Make sure you added `http://localhost:5000/auth/callback` in Intuit Developer Portal

**Issue: Build errors**
- Solution: Run `dotnet restore` to ensure all packages are installed

**Issue: Port already in use**
- Solution: Stop other instances or change the port in `Properties/launchSettings.json`

## Resources

- [Intuit Developer Portal](https://developer.intuit.com)
- [QuickBooks Online API Documentation](https://developer.intuit.com/app/developer/qbo/docs)
- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)

## License

See LICENSE file for details.
