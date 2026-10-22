#!/usr/bin/env bash
# Blocks until the open Editor's EditMode run (EditModeTestRunner.Run via the unity-mcp bridge) is done,
# then prints Temp/editmode-results.txt. Exit status is 0 only when it reads `failed=0`.
#
#   dev/wait-editmode.sh [timeout-seconds]      (default 300)
#
# Call it after the RunCommand that started the run returned: Run writes RUNNING before it returns, so a
# DONE seen here is this run's. Start it with run_in_background (or under Monitor); a bare `sleep` in the
# Bash tool is rejected by the harness.
set -u

results="$(git rev-parse --show-toplevel)/Temp/editmode-results.txt"
deadline=$((SECONDS + ${1:-300}))

until grep -q '^DONE' "$results" 2>/dev/null; do
  if [ "$SECONDS" -ge "$deadline" ]; then
    echo "Timed out after ${1:-300}s; the file reads: $(head -1 "$results" 2>/dev/null || echo '(missing)')"
    echo "A compile error or a paused Editor stalls the run: check Unity_GetConsoleLogs."
    exit 1
  fi
  sleep 2
done

cat "$results"
grep -q 'failed=0' "$results"
