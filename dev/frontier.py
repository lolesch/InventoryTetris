"""Print the open GitHub issues that are ready to build: every `Blocked by` reference is closed.

Usage: python dev/frontier.py
Reads `## Blocked by` from each open issue body via `gh`, so the frontier is derived from the
tracker instead of recorded in a doc. Epics (no `ready-for-agent` label) are listed separately.
"""
import json
import re
import subprocess

issues = json.loads(subprocess.check_output(
    ["gh", "issue", "list", "--state", "open", "--limit", "200",
     "--json", "number,title,labels,body"]))
open_numbers = {i["number"] for i in issues}


def blockers(body):
    section = re.search(r"##\s*Blocked by\s*\n(.*?)(?=\n##|\Z)", body or "", re.S | re.I)
    refs = {int(n) for n in re.findall(r"#(\d+)", section.group(1))} if section else set()
    return sorted(refs & open_numbers)


for issue in sorted(issues, key=lambda i: i["number"]):
    labels = {label["name"] for label in issue["labels"]}
    waiting = blockers(issue["body"])
    if "ready-for-agent" not in labels:
        state = "not ready-for-agent: " + (", ".join(sorted(labels)) or "epic / unlabelled")
    elif waiting:
        state = "blocked by " + ", ".join(f"#{n}" for n in waiting)
    else:
        state = "FRONTIER"
    print(f"#{issue['number']:<4} {state:<40} {issue['title'][:70]}")
