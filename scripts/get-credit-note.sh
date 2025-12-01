#!/bin/bash
# Get a specific credit note (credit memo) by ID from QuickBooks
# Usage: ./scripts/get-credit-note.sh <creditNoteId> [companyId]
# Example: ./scripts/get-credit-note.sh 148 9341455793300229

# Configuration - Update these values based on your QuickBooks setup
DEFAULT_COMPANY_ID="9341455793300229"
BASE_URL="http://localhost:5000"

# Get parameters or use defaults
CREDIT_NOTE_ID="${1:-}"
COMPANY_ID="${2:-$DEFAULT_COMPANY_ID}"

# Validate required parameters
if [ -z "$CREDIT_NOTE_ID" ]; then
    echo "Error: Credit Note ID is required"
    echo "Usage: ./scripts/get-credit-note.sh <creditNoteId> [companyId]"
    echo "Example: ./scripts/get-credit-note.sh 148 9341455793300229"
    exit 1
fi

echo "=========================================="
echo "Getting Credit Note Details"
echo "=========================================="
echo "Credit Note ID: $CREDIT_NOTE_ID"
echo "Company ID: $COMPANY_ID"
echo ""

# Fetch the credit note
echo "Fetching credit note from QuickBooks..."
CREDIT_NOTE_RESPONSE=$(curl -s -X GET "${BASE_URL}/api/credit-notes/${CREDIT_NOTE_ID}?companyId=${COMPANY_ID}")

# Check if request was successful
if echo "$CREDIT_NOTE_RESPONSE" | grep -q '"success" *: *true'; then
    echo "✓ Successfully retrieved credit note"
    echo ""
    
    # Extract key information
    if command -v jq &> /dev/null; then
        DOC_NUMBER=$(echo "$CREDIT_NOTE_RESPONSE" | jq -r '.data.docNumber // "N/A"' 2>/dev/null)
        TOTAL_AMT_RAW=$(echo "$CREDIT_NOTE_RESPONSE" | jq -r '.data.totalAmt // 0' 2>/dev/null)
        # Credit memos have negative TotalAmt in QuickBooks, but we display as positive
        TOTAL_AMT=$(echo "$TOTAL_AMT_RAW" | sed 's/^-//')
        CUSTOMER_NAME=$(echo "$CREDIT_NOTE_RESPONSE" | jq -r '.data.customerRef.name // "N/A"' 2>/dev/null)
        TXN_DATE=$(echo "$CREDIT_NOTE_RESPONSE" | jq -r '.data.txnDate // "N/A"' 2>/dev/null)
        
        echo "=========================================="
        echo "Credit Note Summary"
        echo "=========================================="
        echo "Credit Note ID: $CREDIT_NOTE_ID"
        echo "Document Number: $DOC_NUMBER"
        echo "Customer: $CUSTOMER_NAME"
        echo "Transaction Date: $TXN_DATE"
        echo "Credit Amount: \$$(printf "%.2f" "$TOTAL_AMT" 2>/dev/null || echo "$TOTAL_AMT")"
        echo ""
        
        # Show line items if available
        LINE_COUNT=$(echo "$CREDIT_NOTE_RESPONSE" | jq '.data.line | length' 2>/dev/null || echo "0")
        if [ "$LINE_COUNT" -gt 0 ]; then
            echo "=========================================="
            echo "Line Items ($LINE_COUNT)"
            echo "=========================================="
            echo "$CREDIT_NOTE_RESPONSE" | jq -r '.data.line[]? | "  - \(.description // "N/A"): $\(.amount // 0) (Qty: \(.salesItemLineDetail.qty // 0) × Price: $\(.salesItemLineDetail.unitPrice // 0))"' 2>/dev/null
            echo ""
        fi
    else
        # Basic extraction without jq
        DOC_NUMBER=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"DocNumber" *: *"[^"]*"' | sed 's/.*: *"//;s/".*//')
        TOTAL_AMT_RAW=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"TotalAmt" *: *-[0-9.]*' | sed 's/.*: *//' | tr -d ' ')
        if [ -z "$TOTAL_AMT_RAW" ]; then
            TOTAL_AMT_RAW=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"TotalAmt" *: *[0-9.]*' | sed 's/.*: *//' | tr -d ' ')
        fi
        TOTAL_AMT=$(echo "$TOTAL_AMT_RAW" | sed 's/^-//')
        
        echo "=========================================="
        echo "Credit Note Summary"
        echo "=========================================="
        echo "Credit Note ID: $CREDIT_NOTE_ID"
        echo "Document Number: $DOC_NUMBER"
        echo "Credit Amount: \$${TOTAL_AMT}"
        echo ""
        echo "Tip: Install 'jq' for better output: sudo apt-get install jq"
        echo ""
    fi
    
    # Pretty print the full JSON response
    echo "=========================================="
    echo "Full Credit Note Details (JSON)"
    echo "=========================================="
    if command -v jq &> /dev/null; then
        echo "$CREDIT_NOTE_RESPONSE" | jq '.'
    else
        echo "$CREDIT_NOTE_RESPONSE"
    fi
    
    echo ""
    echo "=========================================="
    echo "Quick Actions"
    echo "=========================================="
    echo "List all credit notes:"
    echo "  ./scripts/list-credit-notes.sh ${COMPANY_ID}"
    echo ""
    echo "View in QuickBooks Portal:"
    echo "  Sales → Products and Services → Credit Memos tab"
    echo "  Or: Sales → All Sales → Filter by 'Credit Memos'"
    echo ""
    
else
    echo "✗ Error: Failed to fetch credit note"
    ERROR_MSG=$(echo "$CREDIT_NOTE_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4)
    if [ -n "$ERROR_MSG" ]; then
        echo "  Error: $ERROR_MSG"
    else
        echo "$CREDIT_NOTE_RESPONSE"
    fi
    exit 1
fi

echo ""
echo "=========================================="
echo "QuickBooks Portal Location"
echo "=========================================="
echo "To view this credit note in QuickBooks Online:"
echo "  1. Go to Sales menu → Products and Services"
echo "  2. Click on 'Credit Memos' tab"
echo "  3. Or go to Sales → All Sales → Filter by 'Credit Memos'"
echo ""
echo "Done!"

