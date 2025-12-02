#!/bin/bash
# EXPERIMENTAL: Settle an invoice using credit note (credit memo)
# WARNING: This is experimental and not recommended for production use
# This uses a hybrid approach: credit note + payment to settle invoice
# Usage: ./scripts/settle-invoice.sh <invoiceId> <settlementAmount> [companyId]
# Example: ./scripts/settle-invoice.sh 147 16567.00 9341455793300229

# Configuration - Update these values based on your QuickBooks setup
DEFAULT_COMPANY_ID="9341455793300229"
BASE_URL="http://localhost:5000"

# Get parameters or use defaults
INVOICE_ID="${1:-}"
SETTLEMENT_AMOUNT="${2:-}"
COMPANY_ID="${3:-$DEFAULT_COMPANY_ID}"

# Validate required parameters
if [ -z "$INVOICE_ID" ] || [ -z "$SETTLEMENT_AMOUNT" ]; then
    echo "Error: Invoice ID and settlement amount are required"
    echo "Usage: ./scripts/settle-invoice.sh <invoiceId> <settlementAmount> [companyId]"
    echo "Example: ./scripts/settle-invoice.sh 147 16567.00 9341455793300229"
    exit 1
fi

# Validate settlement amount is a positive number
if ! echo "$SETTLEMENT_AMOUNT" | grep -qE '^[0-9]+\.?[0-9]*$'; then
    echo "Error: Settlement amount must be a positive number"
    exit 1
fi

echo "=========================================="
echo "Settling Invoice (Credit Note + Payment)"
echo "=========================================="
echo "This uses a hybrid approach: credit note + payment to settle invoice"
echo ""
echo "Invoice ID: $INVOICE_ID"
echo "Settlement Amount: \$${SETTLEMENT_AMOUNT}"
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
    
    # Validate settlement amount doesn't exceed balance
    BALANCE_CHECK=$(echo "$ORIGINAL_BALANCE $SETTLEMENT_AMOUNT" | awk '{if ($2 > $1) print "exceeds"; else print "ok"}')
    if [ "$BALANCE_CHECK" = "exceeds" ]; then
        echo "⚠ Warning: Settlement amount (\$${SETTLEMENT_AMOUNT}) exceeds invoice balance (\$${ORIGINAL_BALANCE})"
        echo "  The settlement will be limited to the invoice balance"
    fi
else
    echo "✗ Error: Failed to fetch invoice or invoice not found"
    echo "$INVOICE_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4 || echo "$INVOICE_RESPONSE"
    exit 1
fi

# Step 2: Create credit note, then payment to apply it to invoice
echo "Step 2: Creating credit note and payment to settle invoice..."
echo "  Step 2a: Creating credit memo"
echo "  Step 2b: Creating payment that applies credit memo to invoice"

# Build description for credit memo (use DocNumber if available, otherwise Invoice ID)
# Properly escape the description to prevent JSON injection
INVOICE_REF="${DOC_NUMBER:-Invoice ${INVOICE_ID}}"
CREDIT_NOTE_DESCRIPTION="Invoice settlement via credit note - ${INVOICE_REF} - Amount: \$${SETTLEMENT_AMOUNT}"

# Escape special JSON characters in the description
# Replace backslashes, quotes, and newlines to prevent JSON injection
ESCAPED_DESCRIPTION=$(echo "$CREDIT_NOTE_DESCRIPTION" | sed 's/\\/\\\\/g' | sed 's/"/\\"/g' | sed 's/$/\\n/' | tr -d '\n' | sed 's/\\n$//')

# Use jq if available for safe JSON construction, otherwise use sed-escaped string
if command -v jq >/dev/null 2>&1; then
    # Use jq for safe JSON construction
    JSON_PAYLOAD=$(jq -n \
        --arg amount "$SETTLEMENT_AMOUNT" \
        --arg txnDate "$(date +%Y-%m-%d)" \
        --arg description "$CREDIT_NOTE_DESCRIPTION" \
        '{Amount: ($amount | tonumber), TxnDate: $txnDate, Description: $description}')
    
    CREDIT_NOTE_RESPONSE=$(curl -s -X POST "${BASE_URL}/api/invoices/${INVOICE_ID}/settle?companyId=${COMPANY_ID}" \
      -H "Content-Type: application/json" \
      -d "$JSON_PAYLOAD")
else
    # Fallback: use sed-escaped string (less safe but better than nothing)
    CREDIT_NOTE_RESPONSE=$(curl -s -X POST "${BASE_URL}/api/invoices/${INVOICE_ID}/settle?companyId=${COMPANY_ID}" \
      -H "Content-Type: application/json" \
      -d "{
    \"Amount\": ${SETTLEMENT_AMOUNT},
    \"TxnDate\": \"$(date +%Y-%m-%d)\",
    \"Description\": \"${ESCAPED_DESCRIPTION}\"
  }")
fi

# Check if credit note creation was successful
if echo "$CREDIT_NOTE_RESPONSE" | grep -q '"success" *: *true'; then
    CREDIT_NOTE_ID=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"Id" *: *"[^"]*"' | head -1 | sed 's/.*: *"//;s/".*//')
    CREDIT_NOTE_TOTAL_RAW=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"TotalAmt" *: *-[0-9.]*' | sed 's/.*: *//' | tr -d ' ')
    if [ -z "$CREDIT_NOTE_TOTAL_RAW" ]; then
        CREDIT_NOTE_TOTAL_RAW=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"TotalAmt" *: *[0-9.]*' | sed 's/.*: *//' | tr -d ' ')
    fi
    CREDIT_NOTE_TOTAL=$(echo "$CREDIT_NOTE_TOTAL_RAW" | sed 's/^-//')
    CREDIT_NOTE_TOTAL=${CREDIT_NOTE_TOTAL:-$SETTLEMENT_AMOUNT}
    
    echo "✓ Credit note created successfully:"
    echo "  Credit Note ID: $CREDIT_NOTE_ID"
    echo "  Credit Amount: \$${CREDIT_NOTE_TOTAL}"
    echo ""
else
    echo "✗ Error: Failed to create credit note for settlement"
    ERROR_MSG=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4)
    if [ -n "$ERROR_MSG" ]; then
        echo "  Error: $ERROR_MSG"
    else
        echo "$CREDIT_NOTE_RESPONSE"
    fi
    exit 1
fi

# Step 3: Fetch the invoice again to check updated balance
echo "Step 3: Verifying invoice settlement status..."
sleep 1  # Brief delay to ensure QuickBooks processes the credit note
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
        echo "✓ Invoice Status: FULLY SETTLED"
        echo "✓ The invoice has been completely settled"
        echo "✓ Remaining Balance: \$${UPDATED_BALANCE}"
    else
        echo "⚠ Invoice Status: PARTIALLY SETTLED"
        echo "⚠ Remaining Balance: \$${UPDATED_BALANCE}"
        echo "⚠ The invoice still has an outstanding balance"
    fi
    
    # Check if hybrid approach worked (balance should have changed)
    if [ "$UPDATED_BALANCE" != "$ORIGINAL_BALANCE" ]; then
        echo ""
        echo "=========================================="
        echo "✅ HYBRID APPROACH SUCCESSFUL"
        echo "=========================================="
        echo "The invoice balance was reduced using credit note + payment."
        echo "This hybrid approach:"
        echo "  1. Created a credit memo"
        echo "  2. Created a payment that links both credit memo and invoice"
        echo "  3. Payment applied the credit memo to the invoice"
        echo ""
        echo "Balance reduced from \$${ORIGINAL_BALANCE} to \$${UPDATED_BALANCE}"
    elif [ "$UPDATED_BALANCE" = "$ORIGINAL_BALANCE" ]; then
        echo ""
        echo "=========================================="
        echo "⚠️  BALANCE UNCHANGED"
        echo "=========================================="
        echo "The invoice balance did NOT change."
        echo "This may indicate the payment linking failed."
        echo "Check QuickBooks portal to verify credit memo and payment status."
        echo ""
    fi
    
    echo ""
    echo "Summary:"
    echo "  Original Balance: \$${ORIGINAL_BALANCE}"
    echo "  Credit Note Amount: \$${CREDIT_NOTE_TOTAL}"
    echo "  New Balance: \$${UPDATED_BALANCE}"
    
    # Calculate amount settled
    AMOUNT_SETTLED=$(echo "$ORIGINAL_BALANCE - $UPDATED_BALANCE" | bc 2>/dev/null || echo "N/A")
    if [ "$AMOUNT_SETTLED" != "N/A" ]; then
        echo "  Amount Settled: \$${AMOUNT_SETTLED}"
    fi
    
else
    echo "✗ Error: Failed to fetch updated invoice status"
    echo "$UPDATED_INVOICE_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4 || echo "$UPDATED_INVOICE_RESPONSE"
    exit 1
fi

echo ""
echo "=========================================="
echo "Settlement Details"
echo "=========================================="
echo "Credit Note ID: $CREDIT_NOTE_ID"
echo "Credit Amount: \$${CREDIT_NOTE_TOTAL}"
echo ""
echo "Next Steps:"
echo "  • View invoice: ${BASE_URL}/api/invoices/${INVOICE_ID}?companyId=${COMPANY_ID}"
echo "  • View credit note: ${BASE_URL}/api/credit-notes/${CREDIT_NOTE_ID}?companyId=${COMPANY_ID}"
echo "  • List all credit notes: ${BASE_URL}/api/credit-notes?companyId=${COMPANY_ID}"
echo ""
echo ""
echo "Settlement Method:"
echo "  1. Credit memo created: \$${CREDIT_NOTE_TOTAL}"
echo "  2. Payment created that applies credit memo to invoice"
echo "  3. Invoice balance reduced: \$${ORIGINAL_BALANCE} → \$${UPDATED_BALANCE}"
echo ""
echo "=========================================="
echo "QuickBooks Portal Location"
echo "=========================================="
echo "To view credit notes in QuickBooks Online:"
echo "  1. Go to Sales menu → Products and Services"
echo "  2. Click on 'Credit Memos' tab"
echo "  3. Or go to Sales → All Sales → Filter by 'Credit Memos'"
echo ""
echo "Note: This settlement uses credit memo + payment approach."
echo ""
echo "Done!"

