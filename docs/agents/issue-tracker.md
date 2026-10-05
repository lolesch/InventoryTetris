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

## Standing notes

- **The Services spec** (`dev/specs/2026-10-01-services-over-providers-design.md`) still says
  "Hero State" and `SessionChanged`. ADR-0015 supersedes that vocabulary: read it as the
  **Hero** and the **World**, and `HeroLoaded`. Its "Sell Basket" is the Sold container
  (ADR-0016).
- **#63** (ground items display) is built: its "shift-quick-move to the floor" criterion is the
  `Hero` context's ground row in #86's table (`QuickMoveIntentKind.Drop`, only while a Run has a ground).
