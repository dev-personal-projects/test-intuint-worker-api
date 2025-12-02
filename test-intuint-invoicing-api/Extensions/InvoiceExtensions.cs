using test_intuint_invoicing_api.Models;

namespace test_intuint_invoicing_api.Extensions;

/// <summary>
/// Extension methods for invoice-related operations
/// </summary>
public static class InvoiceExtensions
{
    /// <summary>
    /// Filters invoice line items to only include SalesItemLineDetail items
    /// </summary>
    /// <param name="lines">The invoice line items to filter</param>
    /// <returns>Filtered list of SalesItemLineDetail line items</returns>
    public static List<LineItem> GetSalesItemLineDetails(this IEnumerable<LineItem>? lines)
    {
        if (lines == null)
            return new List<LineItem>();

        return lines
            .Where(line => line.DetailType == "SalesItemLineDetail" && line.SalesItemLineDetail != null)
            .ToList();
    }

    /// <summary>
    /// Creates adjusted line items for partial settlement based on settlement ratio
    /// </summary>
    /// <param name="lines">The invoice line items to adjust</param>
    /// <param name="settlementRatio">The ratio of settlement (0.0 to 1.0)</param>
    /// <param name="isFullSettlement">Whether this is a full settlement</param>
    /// <returns>List of adjusted line items for credit memo</returns>
    public static List<LineItem> CreateAdjustedLineItemsForSettlement(
        this IEnumerable<LineItem> lines,
        decimal settlementRatio,
        bool isFullSettlement)
    {
        return lines
            .Where(line => line.DetailType == "SalesItemLineDetail" && line.SalesItemLineDetail != null)
            .Select(line =>
            {
                var unitPrice = line.SalesItemLineDetail!.UnitPrice ?? 0;
                var qty = line.SalesItemLineDetail.Qty ?? 0;
                var originalAmount = unitPrice * qty;

                // For partial settlement, proportionally adjust the amount
                var adjustedAmount = isFullSettlement ? originalAmount : originalAmount * settlementRatio;

                // Adjust quantity proportionally for partial settlement
                var adjustedQty = isFullSettlement ? qty : qty * settlementRatio;

                return new LineItem
                {
                    DetailType = line.DetailType,
                    Amount = adjustedAmount > 0 ? adjustedAmount : null,
                    Description = line.Description,
                    SalesItemLineDetail = new SalesItemLineDetail
                    {
                        ItemRef = line.SalesItemLineDetail.ItemRef,
                        Qty = adjustedQty,
                        UnitPrice = unitPrice
                    }
                };
            })
            .ToList();
    }
}

