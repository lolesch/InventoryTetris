# Issue tracker: GitHub

Issues and specs for this repo live as GitHub issues in[`lolesch/InventoryTetris`](https://github.com/lolesch/InventoryTetris/issues). Use the `gh` CLI for all operations.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..."`. Use a heredoc for multi-line bodies.
- **Read an issue**: `gh issue view <number> --comments`, filtering comments by `jq` and also fetching labels.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'` with appropriate `--label` and `--state` filters.
- **Comment on an issue**: `gh issue comment <number> --body "..."`
- **Apply / remove labels**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Close**: `gh issue close <number> --comment "..."`

Infer the repo from `git remote -v` — `gh` does this automatically when run inside a clone.

## Pull requests as a triage surface

**PRs as a request surface: no.** _(Set to `yes` if this repo treats external PRs as feature requests; `/triage` reads this flag.)_

When set to `yes`, PRs run through the same labels and states as issues, using the `gh pr` equivalents:

- **Read a PR**: `gh pr view <number> --comments` and `gh pr diff <number>` for the diff.
- **List external PRs for triage**: `gh pr list --state open --json number,title,body,labels,author,authorAssociation,comments` then keep only `authorAssociation` of `CONTRIBUTOR`, `FIRST_TIME_CONTRIBUTOR`, or `NONE` (drop `OWNER`/`MEMBER`/`COLLABORATOR`).
- **Comment / label / close**: `gh pr comment`, `gh pr edit --add-label`/`--remove-label`, `gh pr close`.

GitHub shares one number space across issues and PRs, so a bare `#42` may be either — resolve with `gh pr view 42` and fall back to `gh issue view 42`.

## When a skill says "publish to the issue tracker"

Create a GitHub issue.

## When a skill says "fetch the relevant ticket"

Run `gh issue view <number> --comments`.

## Frontier

The frontier is **derived, not recorded**: an open `ready-for-agent` issue whose `## Blocked by`
issues are all closed. Run `python dev/frontier.py` (reads the open issues through `gh`).
Nothing in the repo tracks it by hand, so a PR does not carry a tracker commit. An issue is
done when its PR merges and closes it; "built" lives in the issue, the PR, the ADR and `git log`.

Labels carry the rest of an issue's state (`needs-triage`, `ready-for-agent`, `ready-for-human`).

## Wayfinding operations

`/wayfinder` keeps its map and decision tickets on GitHub's **native** sub-issues and issue
dependencies, so the GitHub UI draws the frontier. Implementation tickets keep the
`## Blocked by` body convention above; a decision ticket never carries that section, and an
implementation ticket is never wired with native blocking. `dev/frontier.py` skips `wayfinder:*`
issues for that reason.

The REST calls take an issue's database **id**, not its number:
`gh api repos/lolesch/InventoryTetris/issues/<n> --jq .id`.

- **Create the map**: `gh issue create --label wayfinder:map --title "..." --body-file <file>`.
- **Create a ticket**: `gh issue create --label wayfinder:<research|prototype|grilling|task> ...`,
  then make it a child of the map:
  `gh api -X POST repos/lolesch/InventoryTetris/issues/<map>/sub_issues -F sub_issue_id=<ticket id>`.
- **List the map's tickets**: `gh api "repos/lolesch/InventoryTetris/issues/<map>/sub_issues?per_page=100"`.
- **Block a ticket** (second pass, once every ticket has an id):
  `gh api -X POST repos/lolesch/InventoryTetris/issues/<blocked>/dependencies/blocked_by -F issue_id=<blocker id>`.
  Read with `GET .../issues/<n>/dependencies/blocked_by`; remove with
  `gh api -X DELETE repos/lolesch/InventoryTetris/issues/<blocked>/dependencies/blocked_by/<blocker id>`.
- **Claim a ticket**: `gh issue edit <n> --add-assignee @me`, before any work.
- **Frontier**: `python dev/frontier.py --map <map>` lists the map's open tickets as `FRONTIER`,
  `claimed by`, or `blocked by`.
- **Resolve**: `gh issue comment <n>` with the answer, `gh issue close <n>`, then append the
  context pointer to the map's Decisions so far with `gh issue edit <map> --body-file <file>`.

A spec a map arrives at still goes to `dev/specs/` (CLAUDE.md). A research ticket's subagent
needs the user's yes like any other (CLAUDE.md); without it, resolve the ticket inline.

## Standing notes

- **The Services spec** (`dev/specs/2026-10-01-services-over-providers-design.md`) still says
  "Hero State" and `SessionChanged`. ADR-0015 supersedes that vocabulary: read it as the
  **Hero** and the **World**, and `HeroLoaded`. Its "Sell Basket" is the Sold container
  (ADR-0016).
- **#63** (ground items display) is built: its "shift-quick-move to the floor" criterion is the
  `Hero` context's ground row in #86's table (`QuickMoveIntentKind.Drop`, only while a Run has a ground).
