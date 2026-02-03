#!/bin/sh
# Ensure volume mount points have correct ownership for non-root user
chown -R mymascada:mymascada /app/data /app/logs 2>/dev/null || true

# Drop to non-root user and start the application
exec gosu mymascada dotnet MyMascada.WebAPI.dll "$@"
