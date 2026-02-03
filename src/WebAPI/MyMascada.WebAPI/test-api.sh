#!/bin/bash

echo "Testing MyMascada API..."

# Test health endpoint first
echo "1. Testing health endpoint..."
curl -k -s https://localhost:5126/api/health && echo " ✓ Health endpoint OK" || echo " ✗ Health endpoint failed"

# Test CSV formats endpoint
echo "2. Testing CSV formats endpoint..."
TOKEN="Bearer ${API_TOKEN:?Set API_TOKEN env var with a valid JWT}"

RESPONSE=$(curl -k -s -H "Authorization: $TOKEN" https://localhost:5126/api/CsvImport/formats)
echo "Response: $RESPONSE"

# Check if ANZ is in the response
if echo "$RESPONSE" | grep -q "ANZ"; then
    echo " ✓ ANZ Bank format found in response"
else
    echo " ✗ ANZ Bank format NOT found in response"
fi

# Pretty print the response if it's JSON
if command -v jq &> /dev/null; then
    echo "3. Formatted response:"
    echo "$RESPONSE" | jq .
fi