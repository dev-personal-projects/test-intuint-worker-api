#!/bin/bash
# List all credit notes (credit memos) from QuickBooks
# Usage: ./scripts/list-credit-notes.sh [companyId] [maxResults]
# Example: ./scripts/list-credit-notes.sh 9341455793300229 50

# Configuration - Update these values based on your QuickBooks setup
DEFAULT_COMPANY_ID="9341455793300229"
BASE_URL="http://localhost:5000"

# Get parameters or use defaults
COMPANY_ID="${1:-$DEFAULT_COMPANY_ID}"
MAX_RESULTS="${2:-}"

echo "=========================================="
echo "Listing All Credit Notes"
echo "=========================================="
echo "Company ID: $COMPANY_ID"
if [ -n "$MAX_RESULTS" ]; then
    echo "Max Results: $MAX_RESULTS"
fi
echo ""

# Build the URL with optional maxResults parameter
URL="${BASE_URL}/api/credit-notes?companyId=${COMPANY_ID}"
if [ -n "$MAX_RESULTS" ]; then
    URL="${URL}&maxResults=${MAX_RESULTS}"
fi

# Fetch all credit notes
echo "Fetching credit notes from QuickBooks..."
CREDIT_NOTES_RESPONSE=$(curl -s -X GET "$URL")

# Check if request was successful
if echo "$CREDIT_NOTES_RESPONSE" | grep -q '"success" *: *true'; then
    # Count credit notes in the response
    # Try to extract count using jq if available, otherwise use grep
    if command -v jq &> /dev/null; then
        CREDIT_NOTE_COUNT=$(echo "$CREDIT_NOTES_RESPONSE" | jq '.data | length' 2>/dev/null || echo "0")
    else
        # Simple count using grep - count occurrences of "Id" in data array
        CREDIT_NOTE_COUNT=$(echo "$CREDIT_NOTES_RESPONSE" | grep -o '"Id" *: *"[^"]*"' | wc -l)
    fi
    
    echo "✓ Successfully retrieved credit notes"
    echo ""
    echo "=========================================="
    echo "Summary"
    echo "=========================================="
    echo "Total Credit Notes: $CREDIT_NOTE_COUNT"
    echo ""
    
    # Pretty print the JSON response using jq if available, otherwise show raw JSON
    if command -v jq &> /dev/null; then
        echo "=========================================="
        echo "Credit Note Details (JSON)"
        echo "=========================================="
        echo "$CREDIT_NOTES_RESPONSE" | jq '.'
    else
        echo "=========================================="
        echo "Credit Note Details (JSON)"
        echo "=========================================="
        echo "$CREDIT_NOTES_RESPONSE"
        echo ""
        echo "Tip: Install 'jq' for better JSON formatting: sudo apt-get install jq"
    fi
    
    # Extract and display credit note IDs and basic info
    if [ "$CREDIT_NOTE_COUNT" -gt 0 ]; then
        echo ""
        echo "=========================================="
        echo "Credit Note List"
        echo "=========================================="
        
        # Try to extract credit note IDs and DocNumbers using jq
        if command -v jq &> /dev/null; then
            echo "$CREDIT_NOTES_RESPONSE" | jq -r '.data[]? | "ID: \(.Id // "N/A") | DocNumber: \(.DocNumber // "N/A") | Total: $\(.TotalAmt // 0) | Customer: \(.CustomerRef.Name // "N/A") | Date: \(.TxnDate // "N/A")"'
        else
            echo "Install 'jq' for detailed credit note listing"
            echo "Basic credit note IDs found in JSON above"
        fi
    fi
    
    echo ""
    echo "=========================================="
    echo "Quick Actions"
    echo "=========================================="
    echo "Get specific credit note:"
    echo "  curl -X GET \"${BASE_URL}/api/credit-notes/CREDIT_NOTE_ID?companyId=${COMPANY_ID}\""
    echo ""
    echo "Create credit note for invoice:"
    echo "  curl -X POST \"${BASE_URL}/api/invoices/INVOICE_ID/credit-note?companyId=${COMPANY_ID}\""
    echo ""
    echo "View in QuickBooks Portal:"
    echo "  Sales → Products and Services → Credit Memos tab"
    echo "  Or: Sales → All Sales → Filter by 'Credit Memos'"
    echo ""
    
else
    echo "✗ Error: Failed to fetch credit notes"
    ERROR_MSG=$(echo "$CREDIT_NOTES_RESPONSE" | grep -o '"error":"[^"]*"' | cut -d'"' -f4)
    if [ -n "$ERROR_MSG" ]; then
        echo "  Error: $ERROR_MSG"
    else
        echo "$CREDIT_NOTES_RESPONSE"
    fi
    exit 1
fi

echo ""
echo "=========================================="
echo "QuickBooks Portal Location"
echo "=========================================="
echo "To view credit notes in QuickBooks Online:"
echo "  1. Go to Sales menu → Products and Services"
echo "  2. Click on 'Credit Memos' tab"
echo "  3. Or go to Sales → All Sales → Filter by 'Credit Memos'"
echo ""
echo "Done!"

