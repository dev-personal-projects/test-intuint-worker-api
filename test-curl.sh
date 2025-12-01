#!/bin/bash
# Comprehensive invoice creation example for Shipht It Company
# Usage: ./test-curl.sh
# Note: Update companyId, CustomerRef, and ItemRef values based on your QuickBooks setup

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

curl -X POST "http://localhost:5000/api/invoices?companyId=9341455793300229" \
  -H "Content-Type: application/json" \
  -d "{
    \"CustomerRef\": { \"value\": \"1\", \"name\": \"Shipht It Company\" },
    \"DocNumber\": \"INV-$(date +%Y%m%d)-001\",
    \"TxnDate\": \"${TXN_DATE}\",
    \"DueDate\": \"${DUE_DATE}\",
    \"Line\": [
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 5000.00,
        \"Description\": \"Custom Software Development - E-commerce Platform Module\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"2\", \"name\": \"Software Development\" },
          \"Qty\": 40,
          \"UnitPrice\": 125.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 1200.00,
        \"Description\": \"Cloud Hosting & Infrastructure Setup - AWS/Azure Configuration\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"3\", \"name\": \"Cloud Hosting\" },
          \"Qty\": 1,
          \"UnitPrice\": 1200.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 2500.00,
        \"Description\": \"API Integration Services - Payment Gateway & Shipping APIs\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"4\", \"name\": \"API Integration\" },
          \"Qty\": 25,
          \"UnitPrice\": 100.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 1800.00,
        \"Description\": \"Technical Consulting - Architecture Review & Optimization\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"5\", \"name\": \"Technical Consulting\" },
          \"Qty\": 12,
          \"UnitPrice\": 150.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 900.00,
        \"Description\": \"Database Design & Migration Services\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"6\", \"name\": \"Database Services\" },
          \"Qty\": 6,
          \"UnitPrice\": 150.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 600.00,
        \"Description\": \"Security Audit & Penetration Testing\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"7\", \"name\": \"Security Services\" },
          \"Qty\": 1,
          \"UnitPrice\": 600.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 1500.00,
        \"Description\": \"DevOps Setup - CI/CD Pipeline Configuration\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"8\", \"name\": \"DevOps Services\" },
          \"Qty\": 10,
          \"UnitPrice\": 150.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 800.00,
        \"Description\": \"Mobile App Development - iOS & Android Native Apps\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"9\", \"name\": \"Mobile Development\" },
          \"Qty\": 8,
          \"UnitPrice\": 100.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 450.00,
        \"Description\": \"UI/UX Design Services - User Interface & Experience Design\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"10\", \"name\": \"Design Services\" },
          \"Qty\": 15,
          \"UnitPrice\": 30.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 1200.00,
        \"Description\": \"Maintenance & Support Package - Monthly Retainer (3 months)\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"11\", \"name\": \"Maintenance & Support\" },
          \"Qty\": 3,
          \"UnitPrice\": 400.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 750.00,
        \"Description\": \"Training & Documentation - Team Training Sessions\",
        \"SalesItemLineDetail\": {
          \"ItemRef\": { \"value\": \"12\", \"name\": \"Training Services\" },
          \"Qty\": 5,
          \"UnitPrice\": 150.00
        }
      },
      {
        \"DetailType\": \"SalesItemLineDetail\",
        \"Amount\": 300.00,
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
echo "Invoice created for Shipht It Company"
echo "Total Amount: \$17,000.00"
echo "Transaction Date: ${TXN_DATE}"
echo "Due Date: ${DUE_DATE}"

