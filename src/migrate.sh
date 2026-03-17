#!/bin/sh
# migrate.sh — Run EF Core migration bundle before deploy (Fly release_command)
# This runs in a temporary VM with access to all app secrets.
set -e

echo "=== Running database migrations ==="

CONNECTION_STRING="${ConnectionStrings__DefaultConnection}"

if [ -z "$CONNECTION_STRING" ]; then
  echo "ERROR: ConnectionStrings__DefaultConnection is not set"
  exit 1
fi

echo "Applying pending migrations..."
/app/efbundle --connection "$CONNECTION_STRING"

echo "=== Migrations completed successfully ==="
