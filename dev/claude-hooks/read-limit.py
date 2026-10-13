#!/usr/bin/env python3
"""PreToolUse hook for Read: rejects a whole-file Read of a long .cs file (CLAUDE.md, "Search with Grep").

Exit 2 blocks the call and shows stderr to the agent. A Read with a `limit` always passes, so the
agent reads a long file in ranges; `Edit` accepts a ranged Read as its "has been read".
"""
import json
import sys

MAX_LINES = 400

try:
    call = json.load(sys.stdin)
    args = call.get("tool_input", {})
    path = args.get("file_path", "")
except (ValueError, AttributeError):
    sys.exit(0)

if call.get("tool_name") != "Read" or not path.endswith(".cs") or "limit" in args:
    sys.exit(0)

try:
    with open(path, "rb") as f:
        lines = sum(1 for _ in f)
except OSError:
    sys.exit(0)

if lines > MAX_LINES:
    print(
        f"{path} has {lines} lines. Grep for the symbol, then Read the part you need with offset/limit; "
        f"to read the whole file, take it in ranges of {MAX_LINES} lines.",
        file=sys.stderr,
    )
    sys.exit(2)
