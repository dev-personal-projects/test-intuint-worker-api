#!/bin/bash
# List all invoices from QuickBooks
# Usage: ./scripts/list-invoices.sh [companyId] [maxResults]
# Example: ./scripts/list-invoices.sh 9341455793300229 50

# Configuration - Update these values based on your QuickBooks setup
DEFAULT_COMPANY_ID="9341455793300229"
BASE_URL="http://localhost:5000"

# Get parameters or use defaults
COMPANY_ID="${1:-$DEFAULT_COMPANY_ID}"
MAX_RESULTS="${2:-}"

echo "=========================================="
echo "Listing All Invoices"
echo "=========================================="
echo "Company ID: $COMPANY_ID"
if [ -n "$MAX_RESULTS" ]; then
    echo "Max Results: $MAX_RESULTS"
fi
echo ""

# Build the URL with optional maxResults parameter
URL="${BASE_URL}/api/invoices?companyId=${COMPANY_ID}"
if [ -n "$MAX_RESULTS" ]; then
    URL="${URL}&maxResults=${MAX_RESULTS}"
fi

# Fetch all invoices
echo "Fetching invoices from QuickBooks..."
INVOICES_RESPONSE=$(curl -s -X GET "$URL")

# Check if request was successful
if echo "$INVOICES_RESPONSE" | grep -q '"success" *: *true'; then
    # Extract invoice count and summary information
    # The response structure is: { success: true, data: { invoices: [], count: N, summary: {...} } }
    if command -v jq &> /dev/null; then
        INVOICE_COUNT=$(echo "$INVOICES_RESPONSE" | jq -r '.data.count // (.data.invoices | length) // 0' 2>/dev/null || echo "0")
        TOTAL_AMOUNT=$(echo "$INVOICES_RESPONSE" | jq -r '.data.summary.totalAmount // 0' 2>/dev/null || echo "0")
        TOTAL_BALANCE=$(echo "$INVOICES_RESPONSE" | jq -r '.data.summary.totalBalance // 0' 2>/dev/null || echo "0")
        PAID_COUNT=$(echo "$INVOICES_RESPONSE" | jq -r '.data.summary.paidCount // 0' 2>/dev/null || echo "0")
        UNPAID_COUNT=$(echo "$INVOICES_RESPONSE" | jq -r '.data.summary.unpaidCount // 0' 2>/dev/null || echo "0")
    else
        INVOICE_COUNT=$(echo "$INVOICES_RESPONSE" | grep -o '"count" *: *[0-9]*' | sed 's/.*: *//' | tr -d ' ')
        INVOICE_COUNT=${INVOICE_COUNT:-0}
    fi
    
    echo "✓ Successfully retrieved invoices"
    echo ""
    echo "=========================================="
    echo "Summary"
    echo "=========================================="
    echo "Total Invoices: $INVOICE_COUNT"
    if [ -n "$TOTAL_AMOUNT" ] && [ "$TOTAL_AMOUNT" != "0" ]; then
        echo "Total Amount: \$$(printf "%.2f" "$TOTAL_AMOUNT" 2>/dev/null || echo "$TOTAL_AMOUNT")"
        echo "Total Balance: \$$(printf "%.2f" "$TOTAL_BALANCE" 2>/dev/null || echo "$TOTAL_BALANCE")"
        echo "Paid Invoices: $PAID_COUNT"
        echo "Unpaid Invoices: $UNPAID_COUNT"
    fi
    echo ""
    
    # Pretty print the JSON response using jq if available, otherwise show raw JSON
    if command -v jq &> /dev/null; then
        echo "=========================================="
        echo "Invoice Details (JSON)"
        echo "=========================================="
        echo "$INVOICES_RESPONSE" | jq '.'
    else
        echo "=========================================="
        echo "Invoice Details (JSON)"
        echo "=========================================="
        echo "$INVOICES_RESPONSE"
        echo ""
        echo "Tip: Install 'jq' for better JSON formatting: sudo apt-get install jq"
    fi
    
    # Extract and display invoice IDs and basic info
    if [ "$INVOICE_COUNT" -gt 0 ]; then
        echo ""
        echo "=========================================="
        echo "Invoice List"
        echo "=========================================="
        
        # Try to extract invoice IDs and DocNumbers using grep/sed
        # This is a simple extraction - for better parsing, use jq
        if command -v jq &> /dev/null; then
            echo "$INVOICES_RESPONSE" | jq -r '.data.invoices[]? | "ID: \(.Id // "N/A") | DocNumber: \(.DocNumber // "N/A") | Total: $\(.TotalAmt // 0) | Balance: $\(.Balance // 0) | Customer: \(.CustomerRef.Name // "N/A") | Date: \(.TxnDate // "N/A")"'
        else
            echo "Install 'jq' for detailed invoice listing"
            echo "Basic invoice IDs found in JSON above"
        fi
    fi
    
    echo ""
    echo "=========================================="
    echo "Quick Actions"
    echo "=========================================="
    echo "Get specific invoice:"
    echo "  curl -X GET \"${BASE_URL}/api/invoices/INVOICE_ID?companyId=${COMPANY_ID}\""
    echo ""
    echo "Create new invoice:"
    echo "  ./scripts/create-invoice.sh"
    echo ""
    echo "Settle invoice:"
    echo "  ./scripts/settle-invoice.sh INVOICE_ID PAYMENT_AMOUNT ${COMPANY_ID}"
    echo ""
    
else
    echo "✗ Error: Failed to fetch invoices"
    ERROR_MSG=$(echo "$INVOICES_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4)
    if [ -n "$ERROR_MSG" ]; then
        echo "  Error: $ERROR_MSG"
    else
        echo "$INVOICES_RESPONSE"
    fi
    exit 1
fi

echo "Done!"

