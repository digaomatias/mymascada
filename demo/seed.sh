#!/bin/sh
set -e

API_URL="${API_URL:-http://api:5126}"
DEMO_EMAIL="${DEMO_EMAIL:-demo@mymascada.com}"
DEMO_PASSWORD="${DEMO_PASSWORD:-DemoPass123!}"
DEMO_FIRST_NAME="${DEMO_FIRST_NAME:-Demo}"
DEMO_LAST_NAME="${DEMO_LAST_NAME:-User}"

echo "Waiting for API at ${API_URL}/health ..."
MAX_RETRIES=30
RETRY=0
until curl -sf "${API_URL}/health" > /dev/null 2>&1; do
  RETRY=$((RETRY + 1))
  if [ "$RETRY" -ge "$MAX_RETRIES" ]; then
    echo "ERROR: API did not become healthy after ${MAX_RETRIES} attempts"
    exit 1
  fi
  echo "  Attempt ${RETRY}/${MAX_RETRIES} - waiting 2s..."
  sleep 2
done
echo "API is healthy."

echo "Creating demo user (${DEMO_EMAIL})..."
HTTP_CODE=$(curl -s -o /tmp/seed_response.json -w '%{http_code}' -X POST \
  "${API_URL}/api/testing/create-test-user" \
  -H "Content-Type: application/json" \
  -d "{
    \"email\": \"${DEMO_EMAIL}\",
    \"password\": \"${DEMO_PASSWORD}\",
    \"firstName\": \"${DEMO_FIRST_NAME}\",
    \"lastName\": \"${DEMO_LAST_NAME}\",
    \"createSampleData\": true
  }")

RESPONSE=$(cat /tmp/seed_response.json)

MESSAGE=$(echo "$RESPONSE" | jq -r '.message // empty')

# Treat "already exists" as success -- the desired state is achieved either way
if echo "$MESSAGE" | grep -qi "already exists"; then
  echo "OK: Demo user already exists. Skipping."
  echo "Demo credentials: ${DEMO_EMAIL} / ${DEMO_PASSWORD}"
  exit 0
fi

if [ "$HTTP_CODE" -lt 200 ] || [ "$HTTP_CODE" -ge 300 ]; then
  echo "ERROR: API returned HTTP ${HTTP_CODE}"
  echo "Response: ${RESPONSE}"
  exit 1
fi

IS_SUCCESS=$(echo "$RESPONSE" | jq -r '.isSuccess')

if [ "$IS_SUCCESS" = "true" ]; then
  echo "SUCCESS: ${MESSAGE}"
  echo "Demo credentials: ${DEMO_EMAIL} / ${DEMO_PASSWORD}"
else
  ERRORS=$(echo "$RESPONSE" | jq -r '.errors[]? // empty')
  echo "FAILED: ${MESSAGE}"
  [ -n "$ERRORS" ] && echo "Errors: ${ERRORS}"
  exit 1
fi
