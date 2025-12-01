#!/bin/bash
# Get a specific invoice by ID from QuickBooks
# Usage: ./scripts/get-invoice.sh <invoiceId> [companyId]
# Example: ./scripts/get-invoice.sh 147 9341455793300229

# Configuration - Update these values based on your QuickBooks setup
DEFAULT_COMPANY_ID="9341455793300229"
BASE_URL="http://localhost:5000"

# Get parameters or use defaults
INVOICE_ID="${1:-}"
COMPANY_ID="${2:-$DEFAULT_COMPANY_ID}"

# Validate required parameters
if [ -z "$INVOICE_ID" ]; then
    echo "Error: Invoice ID is required"
    echo "Usage: ./scripts/get-invoice.sh <invoiceId> [companyId]"
    echo "Example: ./scripts/get-invoice.sh 147 9341455793300229"
    exit 1
fi

echo "=========================================="
echo "Getting Invoice Details"
echo "=========================================="
echo "Invoice ID: $INVOICE_ID"
echo "Company ID: $COMPANY_ID"
echo ""

# Fetch the invoice
echo "Fetching invoice from QuickBooks..."
INVOICE_RESPONSE=$(curl -s -X GET "${BASE_URL}/api/invoices/${INVOICE_ID}?companyId=${COMPANY_ID}")

# Check if request was successful
if echo "$INVOICE_RESPONSE" | grep -q '"success" *: *true'; then
    echo "✓ Successfully retrieved invoice"
    echo ""
    
    # Extract key information
    if command -v jq &> /dev/null; then
        DOC_NUMBER=$(echo "$INVOICE_RESPONSE" | jq -r '.data.docNumber // "N/A"' 2>/dev/null)
        TOTAL_AMT=$(echo "$INVOICE_RESPONSE" | jq -r '.data.totalAmt // 0' 2>/dev/null)
        BALANCE=$(echo "$INVOICE_RESPONSE" | jq -r '.data.balance // 0' 2>/dev/null)
        CUSTOMER_NAME=$(echo "$INVOICE_RESPONSE" | jq -r '.data.customerRef.name // "N/A"' 2>/dev/null)
        TXN_DATE=$(echo "$INVOICE_RESPONSE" | jq -r '.data.txnDate // "N/A"' 2>/dev/null)
        DUE_DATE=$(echo "$INVOICE_RESPONSE" | jq -r '.data.dueDate // "N/A"' 2>/dev/null)
        STATUS=$(echo "$INVOICE_RESPONSE" | jq -r '.data.balance // 0' 2>/dev/null | awk '{if ($1 == 0 || $1 == "0") print "PAID"; else print "UNPAID"}')
        
        echo "=========================================="
        echo "Invoice Summary"
        echo "=========================================="
        echo "Invoice ID: $INVOICE_ID"
        echo "Document Number: $DOC_NUMBER"
        echo "Customer: $CUSTOMER_NAME"
        echo "Transaction Date: $TXN_DATE"
        echo "Due Date: $DUE_DATE"
        echo "Total Amount: \$$(printf "%.2f" "$TOTAL_AMT" 2>/dev/null || echo "$TOTAL_AMT")"
        echo "Balance: \$$(printf "%.2f" "$BALANCE" 2>/dev/null || echo "$BALANCE")"
        echo "Status: $STATUS"
        echo ""
        
        # Show line items if available
        LINE_COUNT=$(echo "$INVOICE_RESPONSE" | jq '.data.line | length' 2>/dev/null || echo "0")
        if [ "$LINE_COUNT" -gt 0 ]; then
            echo "=========================================="
            echo "Line Items ($LINE_COUNT)"
            echo "=========================================="
            echo "$INVOICE_RESPONSE" | jq -r '.data.line[]? | "  - \(.description // "N/A"): $\(.amount // 0) (Qty: \(.salesItemLineDetail.qty // 0) × Price: $\(.salesItemLineDetail.unitPrice // 0))"' 2>/dev/null
            echo ""
        fi
    else
        # Basic extraction without jq
        DOC_NUMBER=$(echo "$INVOICE_RESPONSE" | grep -o '"DocNumber" *: *"[^"]*"' | sed 's/.*: *"//;s/".*//')
        TOTAL_AMT=$(echo "$INVOICE_RESPONSE" | grep -o '"TotalAmt" *: *[0-9.-]*' | sed 's/.*: *//' | tr -d ' ')
        BALANCE=$(echo "$INVOICE_RESPONSE" | grep -o '"Balance" *: *[0-9.-]*' | sed 's/.*: *//' | tr -d ' ')
        
        echo "=========================================="
        echo "Invoice Summary"
        echo "=========================================="
        echo "Invoice ID: $INVOICE_ID"
        echo "Document Number: $DOC_NUMBER"
        echo "Total Amount: \$${TOTAL_AMT}"
        echo "Balance: \$${BALANCE}"
        echo ""
        echo "Tip: Install 'jq' for better output: sudo apt-get install jq"
        echo ""
    fi
    
    # Pretty print the full JSON response
    echo "=========================================="
    echo "Full Invoice Details (JSON)"
    echo "=========================================="
    if command -v jq &> /dev/null; then
        echo "$INVOICE_RESPONSE" | jq '.'
    else
        echo "$INVOICE_RESPONSE"
    fi
    
    echo ""
    echo "=========================================="
    echo "Quick Actions"
    echo "=========================================="
    echo "List all invoices:"
    echo "  ./scripts/list-invoices.sh ${COMPANY_ID}"
    echo ""
    echo "Settle this invoice:"
    echo "  ./scripts/settle-invoice.sh ${INVOICE_ID} PAYMENT_AMOUNT ${COMPANY_ID}"
    echo ""
    echo "Create credit note for this invoice:"
    echo "  ./scripts/create-credit-note.sh ${INVOICE_ID} ${COMPANY_ID}"
    echo ""
    
else
    echo "✗ Error: Failed to fetch invoice"
    ERROR_MSG=$(echo "$INVOICE_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4)
    if [ -n "$ERROR_MSG" ]; then
        echo "  Error: $ERROR_MSG"
    else
        echo "$INVOICE_RESPONSE"
    fi
    exit 1
fi

echo "Done!"

