#!/bin/bash
# Create a credit note (credit memo) for an invoice and verify settlement status
# Usage: ./scripts/create-credit-note.sh [invoiceId] [companyId]
# Example: ./scripts/create-credit-note.sh 147 9341455793300229

# Configuration - Update these values based on your QuickBooks setup
DEFAULT_COMPANY_ID="9341455793300229"
BASE_URL="http://localhost:5000"

# Get parameters or use defaults
INVOICE_ID="${1:-}"
COMPANY_ID="${2:-$DEFAULT_COMPANY_ID}"

# Validate invoice ID is provided
if [ -z "$INVOICE_ID" ]; then
    echo "Error: Invoice ID is required"
    echo "Usage: ./scripts/create-credit-note.sh <invoiceId> [companyId]"
    echo "Example: ./scripts/create-credit-note.sh 147 9341455793300229"
    exit 1
fi

echo "=========================================="
echo "Creating Credit Note for Invoice"
echo "=========================================="
echo "Invoice ID: $INVOICE_ID"
echo "Company ID: $COMPANY_ID"
echo ""

# Step 1: Fetch the original invoice to see its current status
echo "Step 1: Fetching original invoice..."
INVOICE_RESPONSE=$(curl -s -X GET "${BASE_URL}/api/invoices/${INVOICE_ID}?companyId=${COMPANY_ID}")

# Check if invoice fetch was successful (handle whitespace variations)
if echo "$INVOICE_RESPONSE" | grep -q '"success" *: *true'; then
    # Extract values handling pretty-printed JSON with spaces
    ORIGINAL_TOTAL=$(echo "$INVOICE_RESPONSE" | grep -o '"TotalAmt" *: *[0-9.-]*' | sed 's/.*: *//' | tr -d ' ')
    ORIGINAL_BALANCE=$(echo "$INVOICE_RESPONSE" | grep -o '"Balance" *: *[0-9.-]*' | sed 's/.*: *//' | tr -d ' ')
    DOC_NUMBER=$(echo "$INVOICE_RESPONSE" | grep -o '"DocNumber" *: *"[^"]*"' | sed 's/.*: *"//;s/".*//')
    
    # Handle null or empty values
    ORIGINAL_TOTAL=${ORIGINAL_TOTAL:-0}
    ORIGINAL_BALANCE=${ORIGINAL_BALANCE:-0}
    
    echo "✓ Invoice found:"
    echo "  Document Number: $DOC_NUMBER"
    echo "  Original Total: \$${ORIGINAL_TOTAL}"
    echo "  Current Balance: \$${ORIGINAL_BALANCE}"
    echo ""
else
    echo "✗ Error: Failed to fetch invoice or invoice not found"
    echo "$INVOICE_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4 || echo "$INVOICE_RESPONSE"
    exit 1
fi

# Step 2: Create credit note for the invoice
echo "Step 2: Creating credit note..."
CREDIT_NOTE_RESPONSE=$(curl -s -X POST "${BASE_URL}/api/invoices/${INVOICE_ID}/credit-note?companyId=${COMPANY_ID}")

# Check if credit note creation was successful (handle whitespace variations)
if echo "$CREDIT_NOTE_RESPONSE" | grep -q '"success" *: *true'; then
    # Extract values handling pretty-printed JSON with spaces
    CREDIT_NOTE_ID=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"Id" *: *"[^"]*"' | head -1 | sed 's/.*: *"//;s/".*//')
    # Credit memos can have negative TotalAmt (QuickBooks stores them as negative), get absolute value for display
    CREDIT_NOTE_TOTAL_RAW=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"TotalAmt" *: *-[0-9.]*' | sed 's/.*: *//' | tr -d ' ')
    # If not found as negative, try positive
    if [ -z "$CREDIT_NOTE_TOTAL_RAW" ]; then
        CREDIT_NOTE_TOTAL_RAW=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"TotalAmt" *: *[0-9.]*' | sed 's/.*: *//' | tr -d ' ')
    fi
    # Get absolute value for display (remove negative sign if present)
    CREDIT_NOTE_TOTAL=$(echo "$CREDIT_NOTE_TOTAL_RAW" | sed 's/^-//')
    
    # Handle null or empty values
    CREDIT_NOTE_TOTAL=${CREDIT_NOTE_TOTAL:-0}
    
    echo "✓ Credit note created successfully:"
    echo "  Credit Note ID: $CREDIT_NOTE_ID"
    echo "  Credit Amount: \$${CREDIT_NOTE_TOTAL}"
    echo ""
else
    echo "✗ Error: Failed to create credit note"
    ERROR_MSG=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4)
    if [ -n "$ERROR_MSG" ]; then
        echo "  Error: $ERROR_MSG"
        # Try to extract QuickBooks error details if available
        if echo "$CREDIT_NOTE_RESPONSE" | grep -q "QuickBooks API error"; then
            echo ""
            echo "  QuickBooks API Error Details:"
            echo "$CREDIT_NOTE_RESPONSE" | grep -o '"Message":"[^"]*"' | sed 's/"Message":"/    - /;s/"$//' | head -3
        fi
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
    # Extract values handling pretty-printed JSON with spaces
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
        echo "✓ The credit note has completely paid off the invoice"
        echo "✓ Remaining Balance: \$${UPDATED_BALANCE}"
    else
        echo "⚠ Invoice Status: PARTIALLY SETTLED"
        echo "⚠ Remaining Balance: \$${UPDATED_BALANCE}"
        echo "⚠ The invoice still has an outstanding balance"
    fi
    
    echo ""
    echo "Summary:"
    echo "  Original Balance: \$${ORIGINAL_BALANCE}"
    echo "  Credit Note Amount: \$${CREDIT_NOTE_TOTAL}"
    echo "  New Balance: \$${UPDATED_BALANCE}"
    
    # Calculate expected balance after credit note
    EXPECTED_BALANCE=$(echo "$ORIGINAL_BALANCE + $CREDIT_NOTE_TOTAL" | bc 2>/dev/null || echo "N/A")
    if [ "$EXPECTED_BALANCE" != "N/A" ]; then
        echo "  Expected Balance: \$${EXPECTED_BALANCE}"
        echo "  Actual Balance: \$${UPDATED_BALANCE}"
    fi
    
else
    echo "✗ Error: Failed to fetch updated invoice status"
    echo "$UPDATED_INVOICE_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4 || echo "$UPDATED_INVOICE_RESPONSE"
    exit 1
fi

echo ""
echo "=========================================="
echo "Credit Note Details"
echo "=========================================="
echo "Credit Note ID: $CREDIT_NOTE_ID"
echo "View credit note: ${BASE_URL}/api/credit-notes/${CREDIT_NOTE_ID}?companyId=${COMPANY_ID}"
echo ""
echo "Done!"

