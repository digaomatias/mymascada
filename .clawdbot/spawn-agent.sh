#!/bin/bash
# spawn-agent.sh — Create a worktree, tmux session, and launch a coding agent
# Usage: spawn-agent.sh <task-id> <branch-name> <agent> <prompt-file|prompt-string> [model] [timeout-minutes]
#
# agent: "claude" or "codex"
# model: optional override (default: claude-opus-4-6 for claude, gpt-5.3-codex for codex)
# timeout-minutes: optional timeout in minutes (default: 120 for claude, 90 for codex)

set -euo pipefail

REPO_ROOT="/Users/rleote/source/mymascada"
WORKTREE_BASE="/Users/rleote/source/mymascada-worktrees"
CLAWDBOT="$REPO_ROOT/.clawdbot"
TASKS_FILE="$CLAWDBOT/active-tasks.json"

REPO_SLUG="digaomatias/mymascada"
TASK_ID="${1:?Usage: spawn-agent.sh <task-id> <branch> <agent> <prompt> [model] [timeout-minutes]}"
BRANCH="${2:?Missing branch name}"
AGENT="${3:?Missing agent: claude or codex}"
PROMPT_INPUT="${4:?Missing prompt (file path or string)}"
MODEL="${5:-}"
TIMEOUT_MINUTES="${6:-}"

# Defaults
if [[ -z "$MODEL" ]]; then
  case "$AGENT" in
    claude) MODEL="claude-opus-4-6" ;;
    codex)  MODEL="gpt-5.3-codex" ;;
    *)      echo "Unknown agent: $AGENT"; exit 1 ;;
  esac
fi

# Timeout defaults (minutes)
if [[ -z "$TIMEOUT_MINUTES" ]]; then
  case "$AGENT" in
    claude) TIMEOUT_MINUTES=120 ;;  # 2 hours for Claude
    codex)  TIMEOUT_MINUTES=90  ;;  # 1.5 hours for Codex
  esac
fi

# Convert to seconds for timeout command
TIMEOUT_SECONDS=$((TIMEOUT_MINUTES * 60))

WORKTREE_DIR="$WORKTREE_BASE/$TASK_ID"
TMUX_SESSION="agent-$TASK_ID"
ORIGINAL_BRANCH="$BRANCH"  # Track original branch for push target

# Resolve prompt
if [[ -f "$PROMPT_INPUT" ]]; then
  PROMPT="$(cat "$PROMPT_INPUT")"
else
  PROMPT="$PROMPT_INPUT"
fi

# Append Definition of Done footer
FOOTER_FILE="$CLAWDBOT/prompt-footer.md"
if [[ -f "$FOOTER_FILE" ]]; then
  PROMPT="$PROMPT

$(cat "$FOOTER_FILE")"
fi

echo "🜁 Spawning agent: $AGENT ($MODEL)"
echo "  Task: $TASK_ID"
echo "  Branch: $BRANCH"
echo "  Worktree: $WORKTREE_DIR"

# 1. Create worktree
cd "$REPO_ROOT"
git fetch origin 2>/dev/null || true

if [[ -d "$WORKTREE_DIR" ]]; then
  echo "  ⚠️  Worktree already exists, reusing"
  # Make sure it's on the right branch
  CURRENT_BRANCH=$(cd "$WORKTREE_DIR" && git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "")
  if [[ -n "$CURRENT_BRANCH" && "$CURRENT_BRANCH" != "$BRANCH" ]]; then
    echo "  🔄 Switching worktree from $CURRENT_BRANCH to $BRANCH"
    (cd "$WORKTREE_DIR" && git checkout "$BRANCH" 2>/dev/null || git checkout -b "$BRANCH" origin/main 2>/dev/null || true)
  fi
else
  # Try several strategies to create the worktree
  if git worktree add "$WORKTREE_DIR" -b "$BRANCH" origin/main 2>/dev/null; then
    echo "  📁 Created worktree with new branch from origin/main"
  elif git worktree add "$WORKTREE_DIR" "$BRANCH" 2>/dev/null; then
    echo "  📁 Created worktree on existing branch $BRANCH"
  elif git worktree add "$WORKTREE_DIR" -b "${TASK_ID}-work" "origin/$BRANCH" 2>/dev/null; then
    # Branch is locked to another worktree — create a local working branch, but push to original
    echo "  📁 Created worktree with working branch ${TASK_ID}-work (will push to $ORIGINAL_BRANCH)"
  elif git worktree add "$WORKTREE_DIR" -b "${TASK_ID}-work" origin/main 2>/dev/null; then
    echo "  📁 Created worktree with working branch ${TASK_ID}-work from origin/main (will push to $ORIGINAL_BRANCH)"
  else
    echo "❌ Failed to create worktree after all strategies"
    exit 1
  fi
fi

# 2. Install dependencies (frontend)
if [[ -f "$WORKTREE_DIR/frontend/package.json" ]]; then
  echo "  📦 Installing frontend dependencies..."
  (cd "$WORKTREE_DIR/frontend" && npm install --silent 2>/dev/null) &
  NPM_PID=$!
fi

# 3. Write prompt to file (avoids shell quoting issues with long prompts)
PROMPT_FILE="$CLAWDBOT/runners/prompt-$TASK_ID.md"
mkdir -p "$CLAWDBOT/runners"
printf '%s' "$PROMPT" > "$PROMPT_FILE"

# Wait for npm if running
if [[ -n "${NPM_PID:-}" ]]; then
  wait "$NPM_PID" 2>/dev/null || true
fi

# 4. Write a runner script (avoids shell quoting issues in tmux)
RUNNER="$CLAWDBOT/runners/run-$TASK_ID.sh"
mkdir -p "$CLAWDBOT/runners"

cat > "$RUNNER" <<'RUNEOF_HEADER'
#!/bin/bash
set -e
RUNEOF_HEADER

cat >> "$RUNNER" <<RUNEOF
cd "$WORKTREE_DIR"
PROMPT_FILE="$PROMPT_FILE"
TIMEOUT_SECONDS=$TIMEOUT_SECONDS
TIMEOUT_MINUTES=$TIMEOUT_MINUTES
RUNEOF

case "$AGENT" in
  claude)
    cat >> "$RUNNER" <<'RUNEOF'
# Run agent with timeout
timeout $TIMEOUT_SECONDS claude --model MODELPLACEHOLDER --dangerously-skip-permissions -p "$(cat "$PROMPT_FILE")"
AGENT_EXIT=$?
if [[ $AGENT_EXIT -eq 124 ]]; then
  echo "⚠️  Agent timed out after $TIMEOUT_MINUTES minutes"
  echo "Proceeding with push/PR for partial work..."
elif [[ $AGENT_EXIT -ne 0 ]]; then
  echo "⚠️  Agent exited with error code $AGENT_EXIT"
fi
RUNEOF
    sed -i '' "s/MODELPLACEHOLDER/$MODEL/" "$RUNNER"
    ;;
  codex)
    cat >> "$RUNNER" <<'RUNEOF'
# Run agent with timeout
timeout $TIMEOUT_SECONDS codex --model MODELPLACEHOLDER -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox "$(cat "$PROMPT_FILE")"
AGENT_EXIT=$?
if [[ $AGENT_EXIT -eq 124 ]]; then
  echo "⚠️  Agent timed out after $TIMEOUT_MINUTES minutes"
  echo "Proceeding with push/PR for partial work..."
elif [[ $AGENT_EXIT -ne 0 ]]; then
  echo "⚠️  Agent exited with error code $AGENT_EXIT"
fi
RUNEOF
    sed -i '' "s/MODELPLACEHOLDER/$MODEL/" "$RUNNER"
    ;;
esac

# Append post-agent push/PR steps
cat >> "$RUNNER" <<RUNEOF

# After agent finishes: push to ORIGINAL branch and create/update PR
echo ""
echo "=== Agent finished. Pushing and creating PR... ==="
LOCAL_BRANCH=\$(git rev-parse --abbrev-ref HEAD)
PUSH_TARGET="$ORIGINAL_BRANCH"
echo "  Local branch: \$LOCAL_BRANCH → pushing to: \$PUSH_TARGET"
git push origin "\$LOCAL_BRANCH:\$PUSH_TARGET" -u --force-with-lease 2>/dev/null || \
  git push origin "\$LOCAL_BRANCH:\$PUSH_TARGET" -u -f 2>/dev/null || true
PR_URL=""
if command -v gh &>/dev/null && gh auth status &>/dev/null 2>&1; then
  # Check if PR already exists for this branch
  PR_URL=\$(gh pr view "\$PUSH_TARGET" --repo "$REPO_SLUG" --json url -q '.url' 2>/dev/null || echo "")
  if [[ -z "\$PR_URL" ]]; then
    PR_URL=\$(gh pr create --repo "$REPO_SLUG" --fill --head "\$PUSH_TARGET" --base main 2>/dev/null || echo "")
  fi
  [[ -z "\$PR_URL" ]] && PR_URL="PR unknown"
fi

# Notify: system event (for Morpheus) + direct Telegram message (for Rodrigo)
openclaw system event --text "Agent $TASK_ID ($AGENT) finished. Branch: $ORIGINAL_BRANCH. PR: \${PR_URL:-none}" --mode now 2>/dev/null || true
BOT_TOKEN=\$(python3 -c "import json; print(json.load(open('/Users/rleote/.openclaw/secrets.json'))['channels']['telegram']['botToken'])" 2>/dev/null || echo "")
NOTIFY() {
  [[ -n "\$BOT_TOKEN" ]] && curl -s "https://api.telegram.org/bot\${BOT_TOKEN}/sendMessage" \
    -d chat_id=7146025092 -d parse_mode=Markdown --data-urlencode "text=\$1" >/dev/null 2>&1 || true
}
NOTIFY "🤖 Agent *$TASK_ID* finished. Branch: \\\`$ORIGINAL_BRANCH\\\`. PR: \${PR_URL:-none}"

# Wait for CI and notify result
if [[ -n "\$PR_URL" && "\$PR_URL" != "none" && "\$PR_URL" != "PR unknown" ]]; then
  PR_NUM=\$(echo "\$PR_URL" | grep -o '[0-9]*\$')
  if [[ -n "\$PR_NUM" ]]; then
    echo "=== Waiting for CI on PR #\$PR_NUM... ==="
    for i in \$(seq 1 30); do
      sleep 20
      CI_STATUS=\$(gh pr checks "\$PR_NUM" --repo "$REPO_SLUG" 2>/dev/null | grep -c "fail" || echo "0")
      CI_PASS=\$(gh pr checks "\$PR_NUM" --repo "$REPO_SLUG" 2>/dev/null | grep -c "pass" || echo "0")
      CI_PENDING=\$(gh pr checks "\$PR_NUM" --repo "$REPO_SLUG" 2>/dev/null | grep -c "pending" || echo "0")
      if [[ "\$CI_PENDING" == "0" ]]; then
        if [[ "\$CI_STATUS" == "0" ]]; then
          NOTIFY "✅ CI passed for PR #\$PR_NUM (*$TASK_ID*). Ready for review."
        else
          NOTIFY "❌ CI failed for PR #\$PR_NUM (*$TASK_ID*). Check: \$PR_URL"
        fi
        break
      fi
    done
  fi
fi
# Trigger Obsidian vault sync
echo "=== Syncing Obsidian vault... ==="
OPENCLAW="/Users/rleote/.nvm/versions/node/v22.16.0/bin/openclaw"
SYNC_PROMPT="You are a documentation sync agent. A coding sub-agent just completed work on MyMascada.

## Task: $TASK_ID
## Branch: $ORIGINAL_BRANCH
## PR: \${PR_URL:-none}

## Your Job
Update the Obsidian vault notes to reflect this completed work:

1. First run: brctl download \"/Users/rleote/Library/Mobile Documents/iCloud~md~obsidian/Documents/Projects/MyMascada/Mobile/Roadmap.md\"
   Then wait 2 seconds, then read it.
   Update Roadmap.md with completed items (check off tasks, move phases forward).

2. Run: brctl download \"/Users/rleote/Library/Mobile Documents/iCloud~md~obsidian/Documents/Projects/MyMascada/Mobile/Development Log.md\"
   Then wait 2 seconds, then read it.
   Add a new entry to the Development Log with today's date and what was accomplished.

3. Be concise — just update the relevant checkboxes and add a short log entry.
4. Do NOT create new files. Only update existing ones.

IMPORTANT: Use brctl download before reading each file (iCloud may have evicted it)."

\$OPENCLAW cron add --in 10s --label "obsidian-sync-$TASK_ID" --model sonnet --task "\$SYNC_PROMPT" 2>/dev/null || true
echo "=== AGENT COMPLETE ==="
RUNEOF
chmod +x "$RUNNER"

# 5. Kill existing session if any
tmux kill-session -t "$TMUX_SESSION" 2>/dev/null || true
# Also kill any leftover background process
PIDFILE="$CLAWDBOT/runners/pid-$TASK_ID"
if [[ -f "$PIDFILE" ]]; then
  kill -9 "$(cat "$PIDFILE")" 2>/dev/null || true
  rm -f "$PIDFILE"
fi

# 6. Launch agent
# Use nohup + background by default (PTY-safe, no tmux suspend issues)
# Set SPAWN_MODE=tmux to use tmux instead
SPAWN_MODE="${SPAWN_MODE:-background}"

if [[ "$SPAWN_MODE" == "tmux" ]]; then
  tmux new-session -d -s "$TMUX_SESSION" "$RUNNER"
  echo "  ✅ Agent running in tmux session: $TMUX_SESSION"
  echo "  Monitor: tmux attach -t $TMUX_SESSION"
else
  LOG_FILE="$CLAWDBOT/runners/log-$TASK_ID.log"
  nohup bash "$RUNNER" > "$LOG_FILE" 2>&1 &
  echo $! > "$PIDFILE"
  echo "  ✅ Agent running in background (PID: $(cat "$PIDFILE"))"
  echo "  Log: tail -f $LOG_FILE"
fi

# 7. Register task
TIMESTAMP=$(date +%s)000
TASK_JSON=$(cat <<EOF
{
  "id": "$TASK_ID",
  "tmuxSession": "$TMUX_SESSION",
  "agent": "$AGENT",
  "model": "$MODEL",
  "timeoutMinutes": $TIMEOUT_MINUTES,
  "description": "",
  "repo": "mymascada",
  "worktree": "$TASK_ID",
  "branch": "$BRANCH",
  "startedAt": $TIMESTAMP,
  "status": "running",
  "retries": 0,
  "maxRetries": 3,
  "notifyOnComplete": true
}
EOF
)

# Add to tasks array
TMP=$(mktemp)
jq --argjson task "$TASK_JSON" '. += [$task]' "$TASKS_FILE" > "$TMP" && mv "$TMP" "$TASKS_FILE"

# Enable the monitoring cron
MONITOR_CRON_ID="6184482f-c08e-4498-8d33-5a99b8039185"
openclaw cron enable "$MONITOR_CRON_ID" 2>/dev/null || true

echo "  📋 Task registered in active-tasks.json"
