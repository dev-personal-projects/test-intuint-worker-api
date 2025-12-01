using test_intuint_invoicing_api.Endpoints;
using test_intuint_invoicing_api.Models;
using test_intuint_invoicing_api.Services;

var builder = WebApplication.CreateBuilder(args);

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
