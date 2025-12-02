using System.Text.Json.Serialization;

namespace test_intuint_invoicing_api.Models;

// Request model for creating a new invoice in QuickBooks
public class InvoiceRequest
{
    [JsonPropertyName("CustomerRef")]
    public Reference? CustomerRef { get; set; }

    [JsonPropertyName("Line")]
    public List<LineItem>? Line { get; set; }

    [JsonPropertyName("DocNumber")]
    public string? DocNumber { get; set; }

    [JsonPropertyName("TxnDate")]
    public string? TxnDate { get; set; }

    [JsonPropertyName("DueDate")]
    public string? DueDate { get; set; }
}

// Request model for creating a credit memo (credit note) in QuickBooks
public class CreditMemoRequest
{
    [JsonPropertyName("CustomerRef")]
    public Reference? CustomerRef { get; set; }

    [JsonPropertyName("Line")]
    public List<LineItem>? Line { get; set; }

    [JsonPropertyName("DocNumber")]
    public string? DocNumber { get; set; }

    [JsonPropertyName("TxnDate")]
    public string? TxnDate { get; set; }

    [JsonPropertyName("PrivateNote")]
    public string? PrivateNote { get; set; }
}

// Line item model for invoice and credit memo line entries
public class LineItem
{
    [JsonPropertyName("DetailType")]
    public string DetailType { get; set; } = "SalesItemLineDetail";

    [JsonPropertyName("Amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("Description")]
    public string? Description { get; set; }

    [JsonPropertyName("SalesItemLineDetail")]
    public SalesItemLineDetail? SalesItemLineDetail { get; set; }
}

// Details for sales item line entries (quantity, unit price, item reference)
public class SalesItemLineDetail
{
    [JsonPropertyName("ItemRef")]
    public Reference? ItemRef { get; set; }

    [JsonPropertyName("Qty")]
    public decimal? Qty { get; set; }

    [JsonPropertyName("UnitPrice")]
    public decimal? UnitPrice { get; set; }
}

// Reference model for linking to QuickBooks entities (customers, items, etc.)
public class Reference
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

// Generic response wrapper from QuickBooks API
public class QuickBooksResponse<T>
{
    [JsonPropertyName("QueryResponse")]
    public QueryResponse<T>? QueryResponse { get; set; }

    [JsonPropertyName("Invoice")]
    public T? Invoice { get; set; }

    [JsonPropertyName("CreditMemo")]
    public T? CreditMemo { get; set; }

    [JsonPropertyName("Payment")]
    public T? Payment { get; set; }

    [JsonPropertyName("Customer")]
    public T? Customer { get; set; }

    [JsonPropertyName("Fault")]
    public Fault? Fault { get; set; }
}

// Query response wrapper for QuickBooks query operations
public class QueryResponse<T>
{
    [JsonPropertyName("Invoice")]
    public List<T>? Invoice { get; set; }

    [JsonPropertyName("CreditMemo")]
    public List<T>? CreditMemo { get; set; }

    [JsonPropertyName("Customer")]
    public List<T>? Customer { get; set; }

    [JsonPropertyName("maxResults")]
    public int? MaxResults { get; set; }
}

// Error fault model from QuickBooks API responses
public class Fault
{
    [JsonPropertyName("Error")]
    public List<ErrorDetail>? Error { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

// Detailed error information from QuickBooks API
public class ErrorDetail
{
    [JsonPropertyName("Message")]
    public string? Message { get; set; }

    [JsonPropertyName("Detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

// Invoice entity model returned from QuickBooks API
public class InvoiceEntity
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("SyncToken")]
    public string? SyncToken { get; set; }

    [JsonPropertyName("CustomerRef")]
    public Reference? CustomerRef { get; set; }

    [JsonPropertyName("Line")]
    public List<LineItem>? Line { get; set; }

    [JsonPropertyName("TotalAmt")]
    public decimal? TotalAmt { get; set; }

    [JsonPropertyName("Balance")]
    public decimal? Balance { get; set; }

    [JsonPropertyName("DocNumber")]
    public string? DocNumber { get; set; }

    [JsonPropertyName("TxnDate")]
    public string? TxnDate { get; set; }

    [JsonPropertyName("DueDate")]
    public string? DueDate { get; set; }
}

// Credit memo entity model returned from QuickBooks API
public class CreditMemoEntity
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("SyncToken")]
    public string? SyncToken { get; set; }

    [JsonPropertyName("CustomerRef")]
    public Reference? CustomerRef { get; set; }

    [JsonPropertyName("Line")]
    public List<LineItem>? Line { get; set; }

    [JsonPropertyName("TotalAmt")]
    public decimal? TotalAmt { get; set; }

    [JsonPropertyName("Balance")]
    public decimal? Balance { get; set; }

    [JsonPropertyName("DocNumber")]
    public string? DocNumber { get; set; }

    [JsonPropertyName("TxnDate")]
    public string? TxnDate { get; set; }
}

// Structured response model for invoice list with metadata and summary
public class InvoiceListResponse
{
    [JsonPropertyName("invoices")]
    public List<InvoiceEntity> Invoices { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("summary")]
    public InvoiceSummary Summary { get; set; } = new();
}

// Summary statistics for invoice list
public class InvoiceSummary
{
    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("totalBalance")]
    public decimal TotalBalance { get; set; }

    [JsonPropertyName("paidAmount")]
    public decimal PaidAmount { get; set; }

    [JsonPropertyName("paidCount")]
    public int PaidCount { get; set; }

    [JsonPropertyName("unpaidCount")]
    public int UnpaidCount { get; set; }
}

// Request model for creating a payment to settle an invoice
public class PaymentRequest
{
    [JsonPropertyName("CustomerRef")]
    public Reference? CustomerRef { get; set; }

    [JsonPropertyName("TotalAmt")]
    public decimal TotalAmt { get; set; }

    [JsonPropertyName("TxnDate")]
    public string? TxnDate { get; set; }

    [JsonPropertyName("Line")]
    public List<PaymentLine>? Line { get; set; }
}

// Payment line item for applying payment to invoice
public class PaymentLine
{
    [JsonPropertyName("Amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("LinkedTxn")]
    public List<LinkedTransaction>? LinkedTxn { get; set; }
}

// Linked transaction reference for payment application
public class LinkedTransaction
{
    [JsonPropertyName("TxnId")]
    public string? TxnId { get; set; }

    [JsonPropertyName("TxnType")]
    public string? TxnType { get; set; }
}

// Request model for invoice settlement (credit note + payment)
public class SettlementRequest
{
    [JsonPropertyName("Amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("TxnDate")]
    public string? TxnDate { get; set; }

    [JsonPropertyName("Description")]
    public string? Description { get; set; }
}

// Payment entity returned from QuickBooks API
public class PaymentEntity
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("SyncToken")]
    public string? SyncToken { get; set; }

    [JsonPropertyName("CustomerRef")]
    public Reference? CustomerRef { get; set; }

    [JsonPropertyName("TotalAmt")]
    public decimal? TotalAmt { get; set; }

    [JsonPropertyName("TxnDate")]
    public string? TxnDate { get; set; }
}

// Request model for creating a customer in QuickBooks
public class CustomerRequest
{
    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("CompanyName")]
    public string? CompanyName { get; set; }
}

// Customer entity returned from QuickBooks API
public class CustomerEntity
{
    [JsonPropertyName("Id")]
    public string? Id { get; set; }

    [JsonPropertyName("SyncToken")]
    public string? SyncToken { get; set; }

    [JsonPropertyName("DisplayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("CompanyName")]
    public string? CompanyName { get; set; }
}

