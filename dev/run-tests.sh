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
# verdict. Set UNITY_EXE to use another editor install. The Editor must not have this project open
# (the run aborts on the project lock), so run it from a worktree or a shadow copy.
set -u

platform="${1:-EditMode}"
filter="${2:-}"
unity="${UNITY_EXE:-C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe}"
root="$(git rev-parse --show-toplevel)"
out="$(cygpath -m "$(mktemp -d)")"

args=(-batchmode -nographics -projectPath "$(cygpath -m "$root")"
      -runTests -testPlatform "$platform" -testResults "$out/results.xml" -logFile "$out/unity.log")

[ -n "$filter" ] && args+=(-testFilter "$filter")

# A PlayMode run boots the game, which continues or creates a hero: point it at scratch, never the player's saves.
[ "$platform" = "PlayMode" ] && args+=(-savesFolder "$out/saves")

"$unity" "${args[@]}" >/dev/null 2>&1

# The run rewrites ProjectSettings; the change is never part of the work.
git -C "$root" checkout -- ProjectSettings 2>/dev/null

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
    print("The Editor has this project open (project lock). Close it, or run from a worktree or shadow copy.")
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
