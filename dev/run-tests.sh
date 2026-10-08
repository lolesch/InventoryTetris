#!/usr/bin/env bash
# Runs this project's Unity tests headless and prints the verdict, so one call replaces the Unity
# command line, the XML parsing and the ProjectSettings clean-up.
#
#   dev/run-tests.sh [EditMode|PlayMode] [test-filter]
#
# The filter is Unity's -testFilter: a test, fixture or namespace name, ';'-separated for several,
# e.g. dev/run-tests.sh EditMode HeroSaveServiceTests
#
# Output is the compile errors (if any), the totals, and each failing test with the first lines of its
# message. Exit status is 0 only when tests ran and none failed; Unity's own exit code is not the
# verdict. Set UNITY_EXE to use another editor install.
#
# The Editor holding this project aborts a run on the project lock. When the lock is held (or with
# --shadow as the first argument) the run happens in a shadow copy instead: Assets, Packages and
# ProjectSettings mirrored into $SHADOW_DIR (default: %TEMP%/<project>-shadow/<checkout name>), which
# keeps its own Library, so only the first run pays the full asset import. The checkout is never touched.
set -u

# Re-run this script under a hidden console. Started without one, every console program below (robocopy,
# git, python) opens its own window and takes keyboard focus; hidden-run.pyw gives them one to inherit.
# Output is replayed when the run ends. RUN_TESTS_VISIBLE=1 skips this.
if [ -z "${RUN_TESTS_HIDDEN:-}" ] && [ -z "${RUN_TESTS_VISIBLE:-}" ] && command -v pythonw >/dev/null 2>&1; then
  hidden_tmp="$(mktemp -d)"
  RUN_TESTS_HIDDEN=1 pythonw "$(cygpath -m "$(dirname "$0")/hidden-run.pyw")" \
    "$(cygpath -m "$hidden_tmp/output.txt")" "$(cygpath -m "$hidden_tmp/status.txt")" \
    "$(cygpath -m "$BASH")" "$(cygpath -m "$0")" "$@"
  cat "$hidden_tmp/output.txt" 2>/dev/null
  status="$(cat "$hidden_tmp/status.txt" 2>/dev/null || echo 1)"
  rm -rf "$hidden_tmp"
  exit "$status"
fi

force_shadow=0
if [ "${1:-}" = "--shadow" ]; then force_shadow=1; shift; fi

platform="${1:-EditMode}"
filter="${2:-}"
unity="${UNITY_EXE:-C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe}"
root="$(git rev-parse --show-toplevel)"
out="$(cygpath -m "$(mktemp -d)")"

# Unity holds Temp/UnityLockfile open while the Editor has the project; an append to it then fails.
locked=0
if [ -e "$root/Temp/UnityLockfile" ] && ! ( : >> "$root/Temp/UnityLockfile" ) 2>/dev/null; then locked=1; fi

project="$root"
if [ "$force_shadow" = 1 ] || [ "$locked" = 1 ]; then
  project="${SHADOW_DIR:-$(cygpath -u "${TEMP:-/tmp}")/$(basename "$root")-shadow/$(git rev-parse --abbrev-ref HEAD | tr '/' '-')}"
  mkdir -p "$project"
  # /MIR deletes what the checkout deleted. Exit codes below 8 are success. .git files are dead weight.
  for dir in Assets Packages ProjectSettings; do
    MSYS2_ARG_CONV_EXCL='*' robocopy "$(cygpath -w "$root/$dir")" "$(cygpath -w "$project/$dir")"       /MIR /XD .git /XF .git /NFL /NDL /NJH /NJS /NP >/dev/null
    [ $? -ge 8 ] && { echo "Could not mirror $dir into the shadow copy at $project"; exit 1; }
  done
  echo "shadow copy: $project"
fi

args=(-batchmode -nographics -projectPath "$(cygpath -m "$project")"
      -runTests -testPlatform "$platform" -testResults "$out/results.xml" -logFile "$out/unity.log")

[ -n "$filter" ] && args+=(-testFilter "$filter")

# A PlayMode run boots the game, which continues or creates a hero: point it at scratch, never the player's saves.
[ "$platform" = "PlayMode" ] && args+=(-savesFolder "$out/saves")

"$unity" "${args[@]}" >/dev/null 2>&1

# The run rewrites ProjectSettings; the change is never part of the work. A shadow copy absorbed it.
[ "$project" = "$root" ] && git -C "$root" checkout -- ProjectSettings 2>/dev/null

python - "$out" <<'PY'
import re
import sys
import xml.etree.ElementTree as ET

out = sys.argv[1]
log = open(out + "/unity.log", encoding="utf-8", errors="replace").read()

errors = sorted(set(re.findall(r"^.*error CS\d+.*$", log, re.M)))
if errors:
    print("COMPILE ERRORS")
    for line in errors[:10]:
        print("  " + line.strip())
    sys.exit(1)

if "another Unity instance is running" in log:
    print("Another Unity instance holds this project (project lock). Close it, or re-run with --shadow.")
    sys.exit(1)

try:
    root = ET.parse(out + "/results.xml").getroot()
except (OSError, ET.ParseError):
    print("No results were written. Read " + out + "/unity.log")
    sys.exit(1)

total, passed, failed = (int(root.get(k)) for k in ("total", "passed", "failed"))
print(f"total={total} passed={passed} failed={failed}   (log: {out}/unity.log)")

for case in root.iter("test-case"):
    if case.get("result") != "Failed":
        continue
    message = (case.findtext("failure/message") or "").strip().splitlines()
    print("FAILED " + case.get("fullname"))
    for line in message[:4]:
        print("    " + line.strip())

sys.exit(0 if total > 0 and failed == 0 else 1)
PY
