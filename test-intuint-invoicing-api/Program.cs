using System.Text.Json;
using System.Text.Json.Serialization;
using test_intuint_invoicing_api.Endpoints;
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

app.MapAuthEndpoints();
app.MapInvoiceEndpoints();
app.MapCreditNoteEndpoints();
app.MapHealthEndpoints();

app.Run();
