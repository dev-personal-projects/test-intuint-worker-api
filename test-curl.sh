#!/bin/bash
# Working curl command for creating an invoice
# Usage: ./test-curl.sh

curl -X POST "http://localhost:5000/api/invoices?companyId=9341455793300229" \
  -H "Content-Type: application/json" \
  -d '{
    "CustomerRef": { "value": "1" },
    "Line": [{
      "DetailType": "SalesItemLineDetail",
      "Amount": 100.00,
      "Description": "Service fee",
      "SalesItemLineDetail": {
        "ItemRef": { "value": "2" },
        "Qty": 1,
        "UnitPrice": 100.00
      }
    }],
    "TxnDate": "2025-01-15",
    "DueDate": "2025-02-15"
  }'

