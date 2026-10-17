#!/usr/bin/env bash
# =============================================================================
# MyMascada - pull-based deploy for Mordor
# =============================================================================
# Run every few minutes by mymascada-deploy.timer. Fetches origin/main and,
# when it moved, builds the images, runs migrations and rolls the stack.
# Nothing outside Mordor can trigger it: it only pulls from GitHub.
#
#   deploy.sh            deploy origin/main if it changed
#   deploy.sh --force    redeploy origin/main now (also clears a manual pin)
#   deploy.sh <ref>      deploy a specific commit/branch and pin it: the timer
#                        leaves it alone until origin/main moves again
#
# If a revision fails to build, migrate or become healthy, the previous
# checkout and images are restored and the revision is recorded in failed-sha,
# then skipped until main moves again (or --force). Migrations are not rolled
# back, so they must stay compatible with the previous release.
# =============================================================================
set -euo pipefail

# Everything runs inside main() so bash parses the whole script before
# executing it: deploy.sh checks out new revisions of itself, and bash
# otherwise keeps reading a script from disk while it runs.
main() {
  APP_HOME="${MYMASCADA_HOME:-/home/rodrigo/apps/mymascada}"
  REPO="$APP_HOME/repo"
  COMPOSE_FILE="$REPO/deploy/mordor/docker-compose.yml"
  DEPLOYED_FILE="$APP_HOME/deployed-sha"
  FAILED_FILE="$APP_HOME/failed-sha"
  PIN_FILE="$APP_HOME/pinned-main"

  exec 9>"$APP_HOME/deploy.lock"
  if ! flock -n 9; then
    echo "Another deploy is running; skipping."
    exit 0
  fi

  local mode=auto ref=origin/main
  case "${1:-}" in
    "") ;;
    --force) mode=force ;;
    *) mode=manual; ref="$1" ;;
  esac

  git -C "$REPO" fetch --quiet --prune origin
  main_sha=$(git -C "$REPO" rev-parse --verify "origin/main^{commit}")
  target=$(git -C "$REPO" rev-parse --verify "$ref^{commit}")
  deployed=$(cat "$DEPLOYED_FILE" 2>/dev/null || true)
  local failed
  failed=$(cat "$FAILED_FILE" 2>/dev/null || true)

  case "$mode" in
    auto)
      if [[ -f "$PIN_FILE" ]]; then
        [[ "$(cat "$PIN_FILE")" == "$main_sha" ]] && exit 0
        echo "main moved since the manual deploy; resuming automatic deploys."
        rm -f "$PIN_FILE"
      fi
      [[ "$target" == "$deployed" ]] && exit 0
      if [[ "$target" == "$failed" ]]; then
        echo "Skipping ${target:0:7}: it failed to deploy before (run with --force to retry)."
        exit 0
      fi
      ;;
    force) rm -f "$PIN_FILE" ;;
  esac

  # Commits from before the Mordor deploy existed would delete this script
  # and the compose file on checkout, leaving the timer with nothing to run.
  local f
  for f in deploy/mordor/docker-compose.yml deploy/mordor/deploy.sh; do
    if ! git -C "$REPO" cat-file -e "$target:$f" 2>/dev/null; then
      echo "Refusing to deploy ${target:0:7}: it has no $f."
      exit 1
    fi
  done

  if [[ "$mode" == manual ]]; then
    echo "$main_sha" > "$PIN_FILE"
  fi

  echo "=== Deploying ${target:0:7} (was ${deployed:0:7}) ==="

  # Keep the images that are running now so a failed deploy can be reverted.
  local svc
  for svc in api web; do
    if docker image inspect "mymascada-$svc:latest" > /dev/null 2>&1; then
      docker tag "mymascada-$svc:latest" "mymascada-$svc:rollback"
    fi
  done

  git -C "$REPO" checkout --quiet --force --detach "$target"

  compose build --pull api web || fail "image build"
  compose up -d --wait postgres || fail "postgres start"
  compose run --rm --no-deps api /app/migrate.sh || fail "migrations"
  compose up -d --wait --remove-orphans || fail "rollout/health checks"
  check_ready || fail "readiness checks"

  echo "$target" > "$DEPLOYED_FILE"
  rm -f "$FAILED_FILE"
  docker image prune -f --filter "until=168h" > /dev/null

  compose ps --format "table {{.Name}}\t{{.Status}}"
  echo "=== Deployed ${target:0:7} ==="
}

compose() {
  MYMASCADA_HOME="$APP_HOME" docker compose \
    --env-file "$APP_HOME/.env" \
    -f "$COMPOSE_FILE" \
    "$@"
}

# The container healthcheck hits /health, which runs no checks at all. Also
# require the database and email checks in /health/ready; llm-service and
# akahu depend on outside services, so they are only reported.
check_ready() {
  local attempt body
  for attempt in 1 2 3 4 5 6; do
    body=$(docker exec mymascada-api curl -s --max-time 10 http://localhost:5126/health/ready || true)
    if printf '%s' "$body" | python3 -c '
import json, sys
try:
    checks = {c["name"]: c["status"] for c in json.load(sys.stdin)["checks"]}
except Exception:
    sys.exit(1)
print("readiness:", ", ".join(f"{k}={v}" for k, v in sorted(checks.items())))
sys.exit(0 if all(checks.get(n) == "Healthy" for n in ("database", "database-ef", "email")) else 1)
'; then
      return 0
    fi
    echo "readiness not met (attempt $attempt/6)"
    sleep 10
  done
  return 1
}

fail() {
  echo "!!! Deploy of ${target:0:7} failed during: $1"
  echo "$target" > "$FAILED_FILE"
  rollback
  exit 1
}

# Put the previous checkout and images back and bring them up again. A no-op
# for containers that never changed (build or migration failures).
rollback() {
  if [[ -z "$deployed" ]]; then
    echo "!!! No previous deployment recorded; leaving the stack as it is."
    return 0
  fi
  echo "--- Rolling back to ${deployed:0:7}"
  git -C "$REPO" checkout --quiet --force --detach "$deployed"
  local svc
  for svc in api web; do
    if docker image inspect "mymascada-$svc:rollback" > /dev/null 2>&1; then
      docker tag "mymascada-$svc:rollback" "mymascada-$svc:latest"
    fi
  done
  if ! compose up -d --wait --remove-orphans; then
    echo "!!! Rollback did not become healthy - check docker ps and container logs."
  fi
}

# Keep the exit on this line: bash must not read past it after main returns.
main "$@"; exit $?
