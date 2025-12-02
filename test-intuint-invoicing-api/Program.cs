using System.Text.Json;
using System.Text.Json.Serialization;
using test_intuint_invoicing_api.Endpoints;
using test_intuint_invoicing_api.Middleware;
using test_intuint_invoicing_api.Models;
using test_intuint_invoicing_api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization to be pretty-printed and use camelCase
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add in-memory caching for customer lookups
builder.Services.AddMemoryCache();

// Configure named HttpClient for QuickBooks API with timeouts and retry policies
builder.Services.AddHttpClient("QuickBooks", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Add default HttpClient for other services
builder.Services.AddHttpClient();

var intuitConfig = builder.Configuration.GetSection("Intuit");
builder.Services.Configure<IntuitSettings>(intuitConfig);

builder.Services.AddScoped<IIntuitOAuthService, IntuitOAuthService>();
builder.Services.AddScoped<IQuickBooksApiClient, QuickBooksApiClient>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("UseHttps", true))
{
    app.UseHttpsRedirection();
}

// Add correlation ID middleware (should be early in the pipeline)
app.UseCorrelationId();

// Add request validation middleware (validates companyId and OAuth tokens)
app.UseRequestValidation();

app.MapAuthEndpoints();
app.MapInvoiceEndpoints();
app.MapCreditNoteEndpoints();
app.MapHealthEndpoints();

app.Run();
