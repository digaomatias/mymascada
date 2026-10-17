#!/usr/bin/env bash
# =============================================================================
# MyMascada - pull-based deploy for Mordor
# =============================================================================
# Run every few minutes by mymascada-deploy.timer. Fetches origin/main and,
# when it moved, builds the images, runs migrations and rolls the stack.
# Nothing outside Mordor can trigger it: it only pulls from GitHub.
#
#   deploy.sh            deploy origin/main if it changed
#   deploy.sh --force    redeploy origin/main even if unchanged
#   deploy.sh <ref>      deploy a specific commit/branch (manual use only)
#
# A revision that fails to build, migrate or become healthy is recorded in
# failed-sha and skipped until main moves again (or --force), so a broken
# commit does not rebuild every five minutes.
# =============================================================================
set -euo pipefail

APP_HOME="${MYMASCADA_HOME:-/home/rodrigo/apps/mymascada}"
REPO="$APP_HOME/repo"
COMPOSE_FILE="$REPO/deploy/mordor/docker-compose.yml"
DEPLOYED_FILE="$APP_HOME/deployed-sha"
FAILED_FILE="$APP_HOME/failed-sha"

exec 9>"$APP_HOME/deploy.lock"
if ! flock -n 9; then
  echo "Another deploy is running; skipping."
  exit 0
fi

force=false
ref=origin/main
case "${1:-}" in
  "") ;;
  --force) force=true ;;
  *) ref="$1"; force=true ;;
esac

git -C "$REPO" fetch --quiet --prune origin
target=$(git -C "$REPO" rev-parse --verify "$ref^{commit}")
deployed=$(cat "$DEPLOYED_FILE" 2>/dev/null || true)
failed=$(cat "$FAILED_FILE" 2>/dev/null || true)

if [[ "$force" == false ]]; then
  [[ "$target" == "$deployed" ]] && exit 0
  if [[ "$target" == "$failed" ]]; then
    echo "Skipping ${target:0:7}: it failed to deploy before (run with --force to retry)."
    exit 0
  fi
fi

echo "=== Deploying ${target:0:7} (was ${deployed:0:7}) ==="
git -C "$REPO" checkout --quiet --force --detach "$target"

compose() {
  MYMASCADA_HOME="$APP_HOME" docker compose \
    --env-file "$APP_HOME/.env" \
    -f "$COMPOSE_FILE" \
    "$@"
}

fail() {
  echo "!!! Deploy of ${target:0:7} failed during: $1"
  echo "$target" > "$FAILED_FILE"
  exit 1
}

compose build --pull api web || fail "image build"
compose up -d --wait postgres || fail "postgres start"
compose run --rm --no-deps api /app/migrate.sh || fail "migrations"
compose up -d --wait --remove-orphans || fail "rollout/health checks"

echo "$target" > "$DEPLOYED_FILE"
rm -f "$FAILED_FILE"
docker image prune -f --filter "until=168h" > /dev/null

compose ps --format "table {{.Name}}\t{{.Status}}"
echo "=== Deployed ${target:0:7} ==="
