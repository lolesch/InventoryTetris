"""Print the open GitHub issues that are ready to build: every `Blocked by` reference is closed.

Usage: python dev/frontier.py [--map <number>]
Reads `## Blocked by` from each open issue body via `gh`, so the frontier is derived from the
tracker instead of recorded in a doc. Epics (no `ready-for-agent` label) are listed separately.
`wayfinder:*` issues are skipped: their blocking is native (docs/agents/issue-tracker.md).
`--map <number>` lists that wayfinder map's open tickets from native sub-issues and dependencies.
"""
import json
import re
import subprocess
import sys

REPO = "repos/lolesch/InventoryTetris/issues"


def gh_api(path):
    return json.loads(subprocess.check_output(["gh", "api", path]))


def map_frontier(map_number):
    tickets = gh_api(f"{REPO}/{map_number}/sub_issues?per_page=100")
    for ticket in sorted(tickets, key=lambda t: t["number"]):
        if ticket["state"] != "open":
            continue
        waiting = [b["number"] for b in gh_api(f"{REPO}/{ticket['number']}/dependencies/blocked_by")
                   if b["state"] == "open"]
        if ticket["assignees"]:
            state = "claimed by " + ", ".join(a["login"] for a in ticket["assignees"])
        elif waiting:
            state = "blocked by " + ", ".join(f"#{n}" for n in sorted(waiting))
        else:
            state = "FRONTIER"
        kind = next((l["name"] for l in ticket["labels"] if l["name"].startswith("wayfinder:")), "")
        print(f"#{ticket['number']:<4} {state:<30} {kind:<20} {ticket['title'][:60]}")


if len(sys.argv) == 3 and sys.argv[1] == "--map":
    map_frontier(int(sys.argv[2]))
    sys.exit()

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
    if any(label.startswith("wayfinder:") for label in labels):
        continue
    waiting = blockers(issue["body"])
    if "ready-for-agent" not in labels:
        state = "not ready-for-agent: " + (", ".join(sorted(labels)) or "epic / unlabelled")
    elif waiting:
        state = "blocked by " + ", ".join(f"#{n}" for n in waiting)
    else:
        state = "FRONTIER"
    print(f"#{issue['number']:<4} {state:<40} {issue['title'][:70]}")
