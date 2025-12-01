#!/bin/bash
# Settle an invoice by applying a payment
# Usage: ./scripts/settle-invoice.sh <invoiceId> <paymentAmount> [companyId]
# Example: ./scripts/settle-invoice.sh 147 16567.00 9341455793300229

# Configuration - Update these values based on your QuickBooks setup
DEFAULT_COMPANY_ID="9341455793300229"
BASE_URL="http://localhost:5000"

# Get parameters or use defaults
INVOICE_ID="${1:-}"
PAYMENT_AMOUNT="${2:-}"
COMPANY_ID="${3:-$DEFAULT_COMPANY_ID}"

# Validate required parameters
if [ -z "$INVOICE_ID" ] || [ -z "$PAYMENT_AMOUNT" ]; then
    echo "Error: Invoice ID and payment amount are required"
    echo "Usage: ./scripts/settle-invoice.sh <invoiceId> <paymentAmount> [companyId]"
    echo "Example: ./scripts/settle-invoice.sh 147 16567.00 9341455793300229"
    exit 1
fi

# Validate payment amount is a positive number
if ! echo "$PAYMENT_AMOUNT" | grep -qE '^[0-9]+\.?[0-9]*$'; then
    echo "Error: Payment amount must be a positive number"
    exit 1
fi

echo "=========================================="
echo "Settling Invoice with Payment"
echo "=========================================="
echo "Invoice ID: $INVOICE_ID"
echo "Payment Amount: \$${PAYMENT_AMOUNT}"
echo "Company ID: $COMPANY_ID"
echo ""

# Step 1: Fetch the original invoice to see its current status
echo "Step 1: Fetching original invoice..."
INVOICE_RESPONSE=$(curl -s -X GET "${BASE_URL}/api/invoices/${INVOICE_ID}?companyId=${COMPANY_ID}")

# Check if invoice fetch was successful
if echo "$INVOICE_RESPONSE" | grep -q '"success" *: *true'; then
    ORIGINAL_TOTAL=$(echo "$INVOICE_RESPONSE" | grep -o '"TotalAmt" *: *[0-9.-]*' | sed 's/.*: *//' | tr -d ' ')
    ORIGINAL_BALANCE=$(echo "$INVOICE_RESPONSE" | grep -o '"Balance" *: *[0-9.-]*' | sed 's/.*: *//' | tr -d ' ')
    DOC_NUMBER=$(echo "$INVOICE_RESPONSE" | grep -o '"DocNumber" *: *"[^"]*"' | sed 's/.*: *"//;s/".*//')
    
    # Handle null or empty values
    ORIGINAL_TOTAL=${ORIGINAL_TOTAL:-0}
    ORIGINAL_BALANCE=${ORIGINAL_BALANCE:-0}
    
    echo "✓ Invoice found:"
    echo "  Document Number: $DOC_NUMBER"
    echo "  Invoice Total: \$${ORIGINAL_TOTAL}"
    echo "  Current Balance: \$${ORIGINAL_BALANCE}"
    echo ""
    
    # Validate payment amount doesn't exceed balance
    BALANCE_CHECK=$(echo "$ORIGINAL_BALANCE $PAYMENT_AMOUNT" | awk '{if ($2 > $1) print "exceeds"; else print "ok"}')
    if [ "$BALANCE_CHECK" = "exceeds" ]; then
        echo "⚠ Warning: Payment amount (\$${PAYMENT_AMOUNT}) exceeds invoice balance (\$${ORIGINAL_BALANCE})"
        echo "  The payment will be limited to the invoice balance"
    fi
else
    echo "✗ Error: Failed to fetch invoice or invoice not found"
    echo "$INVOICE_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4 || echo "$INVOICE_RESPONSE"
    exit 1
fi

# Step 2: Apply payment to the invoice
echo "Step 2: Applying payment to invoice..."
PAYMENT_RESPONSE=$(curl -s -X POST "${BASE_URL}/api/invoices/${INVOICE_ID}/payment?companyId=${COMPANY_ID}" \
  -H "Content-Type: application/json" \
  -d "{
    \"TotalAmt\": ${PAYMENT_AMOUNT},
    \"TxnDate\": \"$(date +%Y-%m-%d)\"
  }")

# Check if payment creation was successful
if echo "$PAYMENT_RESPONSE" | grep -q '"success" *: *true'; then
    PAYMENT_ID=$(echo "$PAYMENT_RESPONSE" | grep -o '"Id" *: *"[^"]*"' | head -1 | sed 's/.*: *"//;s/".*//')
    PAYMENT_AMOUNT_APPLIED=$(echo "$PAYMENT_RESPONSE" | grep -o '"TotalAmt" *: *[0-9.-]*' | sed 's/.*: *//' | tr -d ' ')
    PAYMENT_AMOUNT_APPLIED=${PAYMENT_AMOUNT_APPLIED:-$PAYMENT_AMOUNT}
    
    echo "✓ Payment applied successfully:"
    echo "  Payment ID: $PAYMENT_ID"
    echo "  Payment Amount: \$${PAYMENT_AMOUNT_APPLIED}"
    echo ""
else
    echo "✗ Error: Failed to apply payment"
    ERROR_MSG=$(echo "$PAYMENT_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4)
    if [ -n "$ERROR_MSG" ]; then
        echo "  Error: $ERROR_MSG"
    else
        echo "$PAYMENT_RESPONSE"
    fi
    exit 1
fi

# Step 3: Fetch the invoice again to check updated balance
echo "Step 3: Verifying invoice settlement status..."
sleep 1  # Brief delay to ensure QuickBooks processes the payment
UPDATED_INVOICE_RESPONSE=$(curl -s -X GET "${BASE_URL}/api/invoices/${INVOICE_ID}?companyId=${COMPANY_ID}")

if echo "$UPDATED_INVOICE_RESPONSE" | grep -q '"success" *: *true'; then
    UPDATED_BALANCE=$(echo "$UPDATED_INVOICE_RESPONSE" | grep -o '"Balance" *: *[0-9.-]*' | sed 's/.*: *//' | tr -d ' ')
    UPDATED_TOTAL=$(echo "$UPDATED_INVOICE_RESPONSE" | grep -o '"TotalAmt" *: *[0-9.-]*' | sed 's/.*: *//' | tr -d ' ')
    
    # Handle null or empty values
    UPDATED_BALANCE=${UPDATED_BALANCE:-0}
    UPDATED_TOTAL=${UPDATED_TOTAL:-0}
    
    echo "✓ Invoice status updated:"
    echo "  Invoice Total: \$${UPDATED_TOTAL}"
    echo "  Remaining Balance: \$${UPDATED_BALANCE}"
    echo ""
    
    # Determine settlement status
    echo "=========================================="
    echo "Settlement Status"
    echo "=========================================="
    
    # Compare balance (handle floating point comparison - consider settled if <= 0.01)
    BALANCE_ABS=$(echo "$UPDATED_BALANCE" | awk '{if ($1 < 0) print -$1; else print $1}')
    BALANCE_CHECK=$(echo "$BALANCE_ABS" | awk '{if ($1 <= 0.01) print "settled"; else print "outstanding"}')
    
    if [ "$BALANCE_CHECK" = "settled" ]; then
        echo "✓ Invoice Status: FULLY PAID"
        echo "✓ The invoice has been completely settled"
        echo "✓ Remaining Balance: \$${UPDATED_BALANCE}"
    else
        echo "⚠ Invoice Status: PARTIALLY PAID"
        echo "⚠ Remaining Balance: \$${UPDATED_BALANCE}"
        echo "⚠ The invoice still has an outstanding balance"
    fi
    
    echo ""
    echo "Summary:"
    echo "  Original Balance: \$${ORIGINAL_BALANCE}"
    echo "  Payment Applied: \$${PAYMENT_AMOUNT_APPLIED}"
    echo "  New Balance: \$${UPDATED_BALANCE}"
    
    # Calculate amount paid
    AMOUNT_PAID=$(echo "$ORIGINAL_BALANCE - $UPDATED_BALANCE" | bc 2>/dev/null || echo "N/A")
    if [ "$AMOUNT_PAID" != "N/A" ]; then
        echo "  Amount Paid: \$${AMOUNT_PAID}"
    fi
    
else
    echo "✗ Error: Failed to fetch updated invoice status"
    echo "$UPDATED_INVOICE_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4 || echo "$UPDATED_INVOICE_RESPONSE"
    exit 1
fi

echo ""
echo "=========================================="
echo "Payment Details"
echo "=========================================="
echo "Payment ID: $PAYMENT_ID"
echo "Payment Amount: \$${PAYMENT_AMOUNT_APPLIED}"
echo ""
echo "Done!"

