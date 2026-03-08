#!/bin/bash
# check-agents.sh — Monitor all active agents, check PR/CI status, detect completion
# Outputs JSON status updates. Designed to be called by cron or OpenClaw heartbeat.

set -euo pipefail

REPO_ROOT="/Users/rleote/source/mymascada"
WORKTREE_BASE="/Users/rleote/source/mymascada-worktrees"
CLAWDBOT="$REPO_ROOT/.clawdbot"
TASKS_FILE="$CLAWDBOT/active-tasks.json"
REPO="digaomatias/mymascada"

if [[ ! -f "$TASKS_FILE" ]]; then
  echo "[]"
  exit 0
fi

TASKS=$(cat "$TASKS_FILE")
TASK_COUNT=$(echo "$TASKS" | jq length)

MONITOR_CRON_ID="6184482f-c08e-4498-8d33-5a99b8039185"

if [[ "$TASK_COUNT" == "0" ]]; then
  openclaw cron disable "$MONITOR_CRON_ID" 2>/dev/null || true
  echo '{"activeAgents":0,"alerts":[],"monitorDisabled":true}'
  exit 0
fi

ALERTS=()
UPDATED_TASKS="$TASKS"

for i in $(seq 0 $((TASK_COUNT - 1))); do
  TASK=$(echo "$TASKS" | jq ".[$i]")
  TASK_ID=$(echo "$TASK" | jq -r '.id')
  STATUS=$(echo "$TASK" | jq -r '.status')
  TMUX_SESSION=$(echo "$TASK" | jq -r '.tmuxSession')
  BRANCH=$(echo "$TASK" | jq -r '.branch')
  RETRIES=$(echo "$TASK" | jq -r '.retries')
  MAX_RETRIES=$(echo "$TASK" | jq -r '.maxRetries')
  TIMEOUT_MINUTES=$(echo "$TASK" | jq -r '.timeoutMinutes // 120')
  STARTED_AT=$(echo "$TASK" | jq -r '.startedAt')

  # Skip completed/failed tasks
  if [[ "$STATUS" == "done" || "$STATUS" == "failed" ]]; then
    continue
  fi

  # Check if agent process is alive (try PID file first, then tmux)
  PIDFILE="$CLAWDBOT/runners/pid-$TASK_ID"
  AGENT_ALIVE="no"
  if [[ -f "$PIDFILE" ]] && kill -0 "$(cat "$PIDFILE")" 2>/dev/null; then
    AGENT_ALIVE="yes"
  elif tmux has-session -t "$TMUX_SESSION" 2>/dev/null; then
    AGENT_ALIVE="yes"
  fi

  # Check for timeout if agent is still alive
  if [[ "$AGENT_ALIVE" == "yes" && "$STATUS" == "running" ]]; then
    CURRENT_MS=$(date +%s)000
    ELAPSED_MS=$((CURRENT_MS - STARTED_AT))
    ELAPSED_MINUTES=$((ELAPSED_MS / 60000))
    
    if [[ $ELAPSED_MINUTES -gt $TIMEOUT_MINUTES ]]; then
      echo "  ⏰ Task $TASK_ID has been running for $ELAPSED_MINUTES minutes (timeout: $TIMEOUT_MINUTES). Killing."
      # Kill via PID or tmux
      [[ -f "$PIDFILE" ]] && kill -9 "$(cat "$PIDFILE")" 2>/dev/null && rm -f "$PIDFILE"
      tmux kill-session -t "$TMUX_SESSION" 2>/dev/null || true
      AGENT_ALIVE="no"
      ALERTS+=("{\"task\":\"$TASK_ID\",\"type\":\"agent_timeout\",\"message\":\"Agent for $TASK_ID timed out after $ELAPSED_MINUTES minutes (limit: $TIMEOUT_MINUTES). Session killed.\"}")
      UPDATED_TASKS=$(echo "$UPDATED_TASKS" | jq "(.[] | select(.id == \"$TASK_ID\")) |= . + {\"status\": \"timeout\", \"killedAt\": $CURRENT_MS}")
    fi
  fi

  if [[ "$AGENT_ALIVE" == "no" && ("$STATUS" == "running" || "$STATUS" == "timeout") ]]; then
    # Agent finished — check if there's a PR
    WAS_TIMEOUT=false
    if [[ "$STATUS" == "timeout" ]]; then
      WAS_TIMEOUT=true
    fi
    PR_NUMBER=$(gh pr list --repo "$REPO" --head "$BRANCH" --json number --jq '.[0].number' 2>/dev/null || echo "")

    if [[ -n "$PR_NUMBER" ]]; then
      # PR exists — check CI status
      CI_STATUS=$(gh pr checks "$PR_NUMBER" --repo "$REPO" --json bucket --jq '[.[] | .bucket] | if all(. == "pass") then "pass" elif any(. == "fail") then "fail" else "pending" end' 2>/dev/null || echo "unknown")

      TIMESTAMP=$(date +%s)000

      if [[ "$CI_STATUS" == "pass" ]]; then
        # Check for AI reviewer comments
        GEMINI_REVIEWED=$(gh api "repos/$REPO/pulls/$PR_NUMBER/comments" --jq '[.[] | select(.user.login == "gemini-code-assist[bot]")] | length' 2>/dev/null || echo "0")
        CODEX_REVIEWED=$(gh api "repos/$REPO/issues/$PR_NUMBER/comments" --jq '[.[] | select(.body | test("Codex Code Review"))] | length' 2>/dev/null || echo "0")

        # Trigger Codex review if not done yet
        if [[ "$CODEX_REVIEWED" == "0" ]]; then
          "$CLAWDBOT/review-pr.sh" "$PR_NUMBER" >/dev/null 2>&1 &
          CODEX_REVIEWED="pending"
        fi

        # Build review status summary
        REVIEW_SUMMARY=""
        [[ "$GEMINI_REVIEWED" != "0" ]] && REVIEW_SUMMARY="Gemini ✅" || REVIEW_SUMMARY="Gemini ⏳"
        [[ "$CODEX_REVIEWED" != "0" && "$CODEX_REVIEWED" != "pending" ]] && REVIEW_SUMMARY="$REVIEW_SUMMARY, Codex ✅" || REVIEW_SUMMARY="$REVIEW_SUMMARY, Codex ⏳"

        # Check for unresolved review threads (actual issues flagged by reviewers)
        UNRESOLVED_THREADS=$(gh api graphql -f query="
        {
          repository(owner: \"$(echo $REPO | cut -d/ -f1)\", name: \"$(echo $REPO | cut -d/ -f2)\") {
            pullRequest(number: $PR_NUMBER) {
              reviewThreads(first: 50) {
                nodes { isResolved }
              }
            }
          }
        }" --jq '[.data.repository.pullRequest.reviewThreads.nodes[] | select(.isResolved == false)] | length' 2>/dev/null || echo "0")

        GEMINI_BOOL=$( [[ "$GEMINI_REVIEWED" != "0" ]] && echo true || echo false )
        CODEX_BOOL=$( [[ "$CODEX_REVIEWED" != "0" && "$CODEX_REVIEWED" != "pending" ]] && echo true || echo false )

        if [[ "$UNRESOLVED_THREADS" != "0" && "$UNRESOLVED_THREADS" != "" ]]; then
          # Reviews flagged issues — needs attention before human review
          FINAL_STATUS="review_issues"
          if [[ "$WAS_TIMEOUT" == "true" ]]; then
            FINAL_STATUS="timeout_review_issues"
          fi
          UPDATED_TASKS=$(echo "$UPDATED_TASKS" | jq "(.[] | select(.id == \"$TASK_ID\")) |= . + {
            \"status\": \"$FINAL_STATUS\",
            \"pr\": $PR_NUMBER,
            \"completedAt\": $TIMESTAMP,
            \"checks\": {\"prCreated\": true, \"ciPassed\": true, \"geminiReviewed\": $GEMINI_BOOL, \"codexReviewed\": $CODEX_BOOL, \"unresolvedThreads\": $UNRESOLVED_THREADS}
          }")

          ALERTS+=("{\"task\":\"$TASK_ID\",\"type\":\"pr_has_review_issues\",\"pr\":$PR_NUMBER,\"message\":\"PR #$PR_NUMBER — CI passed but $UNRESOLVED_THREADS unresolved review thread(s). Reviews: $REVIEW_SUMMARY\"}")
        else
          # All clear — ready for human review
          FINAL_STATUS="review"
          if [[ "$WAS_TIMEOUT" == "true" ]]; then
            FINAL_STATUS="timeout_review"
          fi
          UPDATED_TASKS=$(echo "$UPDATED_TASKS" | jq "(.[] | select(.id == \"$TASK_ID\")) |= . + {
            \"status\": \"$FINAL_STATUS\",
            \"pr\": $PR_NUMBER,
            \"completedAt\": $TIMESTAMP,
            \"checks\": {\"prCreated\": true, \"ciPassed\": true, \"geminiReviewed\": $GEMINI_BOOL, \"codexReviewed\": $CODEX_BOOL, \"unresolvedThreads\": 0}
          }")

          ALERTS+=("{\"task\":\"$TASK_ID\",\"type\":\"pr_ready\",\"pr\":$PR_NUMBER,\"message\":\"PR #$PR_NUMBER — CI passed, all review threads resolved. Reviews: $REVIEW_SUMMARY\"}")
        fi

      elif [[ "$CI_STATUS" == "fail" ]]; then
        if [[ "$RETRIES" -lt "$MAX_RETRIES" ]]; then
          ALERTS+=("{\"task\":\"$TASK_ID\",\"type\":\"ci_failed_retry\",\"pr\":$PR_NUMBER,\"message\":\"PR #$PR_NUMBER CI failed. Retry $((RETRIES+1))/$MAX_RETRIES available.\"}")
          FINAL_STATUS="ci_failed"
          if [[ "$WAS_TIMEOUT" == "true" ]]; then
            FINAL_STATUS="timeout_ci_failed"
          fi
          UPDATED_TASKS=$(echo "$UPDATED_TASKS" | jq "(.[] | select(.id == \"$TASK_ID\")) |= . + {\"status\": \"$FINAL_STATUS\", \"pr\": $PR_NUMBER}")
        else
          ALERTS+=("{\"task\":\"$TASK_ID\",\"type\":\"ci_failed_max\",\"pr\":$PR_NUMBER,\"message\":\"PR #$PR_NUMBER CI failed. Max retries reached. Needs human attention.\"}")
          FINAL_STATUS="failed"
          if [[ "$WAS_TIMEOUT" == "true" ]]; then
            FINAL_STATUS="timeout_failed"
          fi
          UPDATED_TASKS=$(echo "$UPDATED_TASKS" | jq "(.[] | select(.id == \"$TASK_ID\")) |= . + {\"status\": \"$FINAL_STATUS\", \"pr\": $PR_NUMBER}")
        fi
      else
        FINAL_STATUS="ci_pending"
        if [[ "$WAS_TIMEOUT" == "true" ]]; then
          FINAL_STATUS="timeout_ci_pending"
        fi
        UPDATED_TASKS=$(echo "$UPDATED_TASKS" | jq "(.[] | select(.id == \"$TASK_ID\")) |= . + {\"status\": \"$FINAL_STATUS\", \"pr\": $PR_NUMBER}")
      fi

    else
      # No PR — agent died without creating one
      if [[ "$WAS_TIMEOUT" == "true" ]]; then
        # Timeout but no PR created
        ALERTS+=("{\"task\":\"$TASK_ID\",\"type\":\"agent_timeout_no_pr\",\"message\":\"Agent for $TASK_ID timed out and didn't create a PR.\"}")
        UPDATED_TASKS=$(echo "$UPDATED_TASKS" | jq "(.[] | select(.id == \"$TASK_ID\")) |= . + {\"status\": \"timeout_no_pr\"}")
      elif [[ "$RETRIES" -lt "$MAX_RETRIES" ]]; then
        ALERTS+=("{\"task\":\"$TASK_ID\",\"type\":\"agent_died\",\"message\":\"Agent for $TASK_ID exited without creating a PR. Retry $((RETRIES+1))/$MAX_RETRIES available.\"}")
        UPDATED_TASKS=$(echo "$UPDATED_TASKS" | jq "(.[] | select(.id == \"$TASK_ID\")) |= . + {\"status\": \"agent_died\"}")
      else
        ALERTS+=("{\"task\":\"$TASK_ID\",\"type\":\"agent_failed\",\"message\":\"Agent for $TASK_ID failed permanently. No PR created after $MAX_RETRIES retries.\"}")
        UPDATED_TASKS=$(echo "$UPDATED_TASKS" | jq "(.[] | select(.id == \"$TASK_ID\")) |= . + {\"status\": \"failed\"}")
      fi
    fi
  fi
done

# Save updated tasks
echo "$UPDATED_TASKS" | jq '.' > "$TASKS_FILE"

# Count active (exclude timeout statuses)
ACTIVE=$(echo "$UPDATED_TASKS" | jq '[.[] | select((.status == "running" or .status == "ci_pending") and (.status | startswith("timeout") | not))] | length')

# Disable monitor cron if nothing active
if [[ "$ACTIVE" == "0" ]]; then
  openclaw cron disable "$MONITOR_CRON_ID" 2>/dev/null || true
fi

# Build alerts JSON
ALERTS_JSON="["
for idx in "${!ALERTS[@]}"; do
  [[ $idx -gt 0 ]] && ALERTS_JSON+=","
  ALERTS_JSON+="${ALERTS[$idx]}"
done
ALERTS_JSON+="]"

echo "{\"activeAgents\":$ACTIVE,\"alerts\":$ALERTS_JSON}"
