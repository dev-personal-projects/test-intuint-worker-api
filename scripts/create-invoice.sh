#!/bin/bash
# Comprehensive invoice creation example
# Usage: ./scripts/create-invoice.sh [customerName] [companyId]
# Example: ./scripts/create-invoice.sh "Shipht It Company" 9341455793300229
# Note: If customer doesn't exist, it will be created automatically
#       Update ItemRef values based on your QuickBooks setup

# Configuration - Update these values based on your QuickBooks setup
DEFAULT_COMPANY_ID="9341455793300229"
DEFAULT_CUSTOMER_NAME="Goodinfo Solutions tech test"
BASE_URL="http://localhost:5000"

# Get parameters or use defaults
CUSTOMER_NAME="${1:-$DEFAULT_CUSTOMER_NAME}"
COMPANY_ID="${2:-$DEFAULT_COMPANY_ID}"

# Get today's date and calculate due date (30 days from today)
TXN_DATE=$(date +%Y-%m-%d)
# Calculate due date (30 days from today) - works on Linux and macOS
if date -d "+30 days" +%Y-%m-%d >/dev/null 2>&1; then
  DUE_DATE=$(date -d "+30 days" +%Y-%m-%d)
elif date -v+30d +%Y-%m-%d >/dev/null 2>&1; then
  DUE_DATE=$(date -v+30d +%Y-%m-%d)
else
  # Fallback: use a simple calculation (may not work on all systems)
  DUE_DATE=$(date +%Y-%m-%d)
fi

echo "=========================================="
echo "Creating Invoice"
echo "=========================================="
echo "Customer Name: $CUSTOMER_NAME"
echo "Company ID: $COMPANY_ID"
echo "Transaction Date: ${TXN_DATE}"
echo "Due Date: ${DUE_DATE}"
echo ""
echo "Note: If customer '$CUSTOMER_NAME' doesn't exist, it will be created automatically"
echo ""

curl -X POST "http://localhost:5000/api/invoices?companyId=${COMPANY_ID}&customerName=$(echo "$CUSTOMER_NAME" | sed 's/ /%20/g')" \
  -H "Content-Type: application/json" \
  -d "{
    \"DocNumber\": \"INV-$(date +%Y%m%d)-001\",
    \"TxnDate\": \"${TXN_DATE}\",
    \"DueDate\": \"${DUE_DATE}\",
    \"Line\": [
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"Custom Software Development - E-commerce Platform Module\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"2\", \"name\": \"Software Development\" },
          \"Qty\": 40,
          \"UnitPrice\": 125.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"Cloud Hosting & Infrastructure Setup - AWS/Azure Configuration\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"3\", \"name\": \"Cloud Hosting\" },
          \"Qty\": 1,
          \"UnitPrice\": 1900.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"API Integration Services - Payment Gateway & Shipping APIs\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"4\", \"name\": \"API Integration\" },
          \"Qty\": 25,
          \"UnitPrice\": 120.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"Technical Consulting - Architecture Review & Optimization\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"5\", \"name\": \"Technical Consulting\" },
          \"Qty\": 12,
          \"UnitPrice\": 150.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"Database Design & Migration Services\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"6\", \"name\": \"Database Services\" },
          \"Qty\": 6,
          \"UnitPrice\": 180.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"Security Audit & Penetration Testing\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"7\", \"name\": \"Security Services\" },
          \"Qty\": 1,
          \"UnitPrice\": 600.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"DevOps Setup - CI/CD Pipeline Configuration\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"8\", \"name\": \"DevOps Services\" },
          \"Qty\": 10,
          \"UnitPrice\": 150.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"Mobile App Development - iOS & Android Native Apps\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"9\", \"name\": \"Mobile Development\" },
          \"Qty\": 8,
          \"UnitPrice\": 110.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"UI/UX Design Services - User Interface & Experience Design\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"10\", \"name\": \"Design Services\" },
          \"Qty\": 15,
          \"UnitPrice\": 40.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"Maintenance & Support Package - Monthly Retainer (3 months)\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"11\", \"name\": \"Maintenance & Support\" },
          \"Qty\": 3,
          \"UnitPrice\": 400.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"Training & Documentation - Team Training Sessions\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"12\", \"name\": \"Training Services\" },
          \"Qty\": 5,
          \"UnitPrice\": 150.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Description\": \"Performance Optimization - Code Review & Refactoring\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"13\", \"name\": \"Optimization Services\" },
          \"Qty\": 2,
          \"UnitPrice\": 150.00
        }
      }
    ]
  }"

echo ""
echo "=========================================="
echo "Invoice Creation Summary"
echo "=========================================="
echo "Customer: $CUSTOMER_NAME"
echo "Transaction Date: ${TXN_DATE}"
echo "Due Date: ${DUE_DATE}"
echo ""
echo "Line Items:"
echo "  - Custom Software Development: 40 hrs × \$125.00 = \$5,000.00"
echo "  - Cloud Hosting Setup: 1 × \$1,200.00 = \$1,200.00"
echo "  - API Integration: 25 hrs × \$100.00 = \$2,500.00"
echo "  - Technical Consulting: 12 hrs × \$150.00 = \$1,800.00"
echo "  - Database Services: 6 hrs × \$150.00 = \$900.00"
echo "  - Security Audit: 1 × \$600.00 = \$600.00"
echo "  - DevOps Setup: 10 hrs × \$150.00 = \$1,500.00"
echo "  - Mobile Development: 8 hrs × \$100.00 = \$800.00"
echo "  - UI/UX Design: 15 hrs × \$30.00 = \$450.00"
echo "  - Maintenance & Support: 3 months × \$400.00 = \$1,200.00"
echo "  - Training: 5 hrs × \$150.00 = \$750.00"
echo "  - Performance Optimization: 2 hrs × \$150.00 = \$300.00"
echo ""
echo "Expected Total: \$17,000.00"
echo ""
echo "Note: Amounts are calculated automatically by QuickBooks from UnitPrice × Qty"

