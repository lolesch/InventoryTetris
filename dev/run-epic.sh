#!/usr/bin/env bash
# Builds an epic's tickets one after another, inline (no subagents), on ONE branch in ONE worktree.
# Every ticket runs in a fresh headless `claude -p` session, so context never accumulates: state lives
# in git and in the issues, not in a conversation. This replaces /implement-spec's subagent fan-out.
#
#   dev/run-epic.sh <epic-number> [--max N]
#
# Per round: pick the lowest-numbered ticket of the epic that `dev/frontier.py` lists as FRONTIER, run
# `/implement #N` in the worktree, check it produced a commit and left the tree clean, close the issue
# (which is what unblocks its dependents), repeat. When no ticket is left, run one /code-review --fix
# over the whole branch. Stops (exit 1) on the first ticket that makes no commit or leaves a dirty tree.
# Never pushes, merges or opens a PR: that stays yours.
#
# Worktree  <repo-parent>/IT-epic-<N>   branch  feature/epic-<N>  (created off `main`, reused if present)
# Logs      <worktree>/Temp/run-epic/<ticket>.log
# Env       CLAUDE_FLAGS   flags for every `claude -p` call. Default: acceptEdits plus an allowlist of git, gh,
#                          python, bash/dev scripts and basic file commands; anything else is denied, since a
#                          headless run cannot answer a prompt. To go unattended without the allowlist, you
#                          opt in yourself: CLAUDE_FLAGS=--dangerously-skip-permissions dev/run-epic.sh 224
#           REVIEW_LEVEL   /code-review level for the final pass (default high)
#
# The first run in a new worktree pays a full Unity asset import (see docs/agents/unity-verification.md).
set -euo pipefail

epic="${1:?usage: dev/run-epic.sh <epic-number> [--max N]}"
shift
max=0
while [ $# -gt 0 ]; do
  case "$1" in
    --max) max="${2:?--max needs a number}"; shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

flags="${CLAUDE_FLAGS---permission-mode acceptEdits --allowedTools Bash(git:*) Bash(gh:*) Bash(python:*) Bash(bash:*) Bash(dev/*) Bash(ls:*) Bash(mkdir:*) Bash(rm:*) Bash(mv:*) Bash(dotnet:*)}"
review_level="${REVIEW_LEVEL:-high}"

main_repo="$(git rev-parse --show-toplevel)"
branch="feature/epic-$epic"
wt="$(dirname "$main_repo")/IT-epic-$epic"

# --- the epic's own tickets: the `| #N |` rows of its "## Tickets" table ---------------------------------
tickets="$(gh issue view "$epic" --json body -q .body | python -c '
import re, sys
body = sys.stdin.read()
section = re.search(r"##\s*Tickets\s*\n(.*?)(?=\n##|\Z)", body, re.S | re.I)
if not section:
    sys.exit("epic has no ## Tickets table")
print(" ".join(sorted(set(re.findall(r"^\|\s*#(\d+)\s*\|", section.group(1), re.M)), key=int)))
')"
[ -n "$tickets" ] || { echo "epic #$epic lists no tickets" >&2; exit 1; }
echo "epic #$epic tickets: $tickets"

# --- the one worktree -------------------------------------------------------------------------------------
if [ ! -d "$wt" ]; then
  if git show-ref --verify --quiet "refs/heads/$branch"; then
    git worktree add "$wt" "$branch"
  else
    git worktree add "$wt" -b "$branch" main
  fi
  git -C "$wt" submodule update --init Assets/Submodules/Utility
fi
cd "$wt"
[ "$(git branch --show-current)" = "$branch" ] || { echo "$wt is not on $branch" >&2; exit 1; }
[ -z "$(git status --porcelain)" ] || { echo "$wt has uncommitted changes; clean it first" >&2; exit 1; }
mkdir -p Temp/run-epic

run_claude() { # <log-name> <prompt>
  # shellcheck disable=SC2086
  # prompt first: --allowedTools is variadic and would swallow a trailing prompt
  claude -p "$2" $flags 2>&1 | tee "Temp/run-epic/$1.log"
}

next_ticket() {
  python dev/frontier.py | python -c '
import re, sys
mine = set(sys.argv[1].split())
for line in sys.stdin:
    m = re.match(r"#(\d+)\s+FRONTIER", line)
    if m and m.group(1) in mine:
        print(m.group(1)); break
' "$tickets"
}

done_count=0
while true; do
  n="$(next_ticket)"
  [ -n "$n" ] || break
  if [ "$max" -gt 0 ] && [ "$done_count" -ge "$max" ]; then
    echo "--max $max reached; next would be #$n"; exit 0
  fi

  before="$(git rev-parse HEAD)"
  echo; echo "=== #$n ($((done_count + 1))) ==="
  run_claude "$n" "/implement #$n

You are in the epic #$epic worktree, already on branch $branch. Build ticket #$n only (gh issue view $n --comments; the epic is #$epic).
- Run everything inline: do not start subagents, do not create branches or worktrees. Commit on this branch with a message that cites #$n.
- Follow CLAUDE.md and docs/agents/. Verify with dev/run-tests.sh. Leave the tree clean.
- Skip /code-review: the driver runs one review over the whole branch at the end.
- Do not push, merge, open a PR or close any issue: the driver closes #$n after checking your commit.
- If the ticket is unclear or blocked, make no commit and say why in your last message." || true

  after="$(git rev-parse HEAD)"
  if [ "$before" = "$after" ]; then echo "#$n: no commit made; see Temp/run-epic/$n.log" >&2; exit 1; fi
  if [ -n "$(git status --porcelain)" ]; then echo "#$n: tree left dirty; see Temp/run-epic/$n.log" >&2; exit 1; fi

  gh issue close "$n" --comment "Built on \`$branch\` at $(git rev-parse --short HEAD) (driver: dev/run-epic.sh)."
  done_count=$((done_count + 1))
done

open="$(gh issue list --state open --json number -q '.[].number' | tr '\n' ' ')"
left=""
for t in $tickets; do case " $open" in *" $t "*) left="$left #$t" ;; esac; done
if [ -n "$left" ]; then
  echo; echo "frontier empty but epic tickets still open:$left (blocked or not ready-for-agent)"; exit 1
fi

echo; echo "=== all tickets built: final review ==="
run_claude "review" "/code-review $review_level --fix

Review the whole branch diff against main ($(git merge-base main HEAD)..HEAD) for epic #$epic (spec in the epic body). Apply the fixes inline, no subagents, commit them on this branch with a fix: message, run dev/run-tests.sh, leave the tree clean. Do not push or merge." || true

[ -z "$(git status --porcelain)" ] || { echo "review left the tree dirty" >&2; exit 1; }
echo; echo "Done: $branch in $wt ($(git rev-list --count main..HEAD) commits). Epic #$epic is still open: merge, then close it."
