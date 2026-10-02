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

## Frontiers — issues vs. implementation

Nineteen open issues (scanned 2026-10-01, `main` @ `1545d90`); the frontier — an issue whose blockers are all closed and whose criteria are not already met by the code — starts at **#107**. #107 is the only unblocked ticket of the Services epic, so the whole epic is a single wire until #108.

**Keep this table current:** update after every `/implement`, and PR-to-`main` by removing closed issues and re-scan the open set.

### Readiness of the open set

| # | Title | Blockers | State vs. this branch |
|---|---|---|---|
| **107** | Services 1: name the per-hero unit, record the service model | #68 (closed) | **Frontier.** Docs-only: `CONTEXT.md` has no Hero State entry and the Session entry still claims the hero/containers/wallet; no ADR for the service model yet (latest is 0014). Honours #68 via ADR-0014. **Spec is not on `main`** — see notes. |
| 108 | Services 2: boot, `GameConfig`, locator, runner | #107 | Blocked. |
| 109 | Services 3: item service replaces `ItemProvider` | #108 | Blocked. Parallel with #110. |
| 110 | Services 4: `Hero` as a plain class | #108 | Blocked. Parallel with #109. |
| 111 | Services 5: hero stat panel binds to `Hero` | #110 | Blocked. |
| 112 | Services 6: the Hero State holder | #109, #110 | Blocked. |
| 113 | Services 7: simulation service on the Hero State | #109, #112 | Blocked. |
| 117 | Services 11: migrate hero callers, delete `DummyTarget` | #111, #112 | Blocked. (Ahead of #114: #114 waits on it.) |
| 114 | Services 8: replace on load, `SessionChanged`, rebinding | #112, #113, #117 | Blocked. |
| 115 | Services 9: migrate inventory callers, GUI | #114 | Blocked. |
| 116 | Services 10: migrate inventory callers, runtime + simulation | #114 (#103 closed) | Blocked. |
| 118 | Services 12: `DebugPanel` replaces UnityEvent buttons | #112, #116 | Blocked. |
| 119 | Services 13: contract, delete the old providers | #115, #116, #117, #113, #118 | Blocked. Closes #46. |
| **106** | Epic: Services over providers | #68 (closed) | Epic. Closes when #119 does. |
| **63** | Ground items display | #116 (new), LootFlow epic (merged) | **Now blocked, no longer the frontier.** The ticket was amended to name the acquisition entry point on the inventory service. Still owes the refresh: its "shift-quick-move to the floor" criterion must become a ground row in #86's context table. |
| **122** | Items with no affixes are worthless | — | **Needs triage** (`needs-triage`). `ItemView.SellValue` sums affix values, so a consumable with none prices at 0: unsellable alone, free on a Supply shelf. Value-model fix, not a basket change. Independent of the epic. |
| **69** | Centralized, rebindable input service | — | **Needs `/to-spec`.** `2026-09-26-session-ux-research.md` §3 + `2026-09-28-save-serialization-prior-art.md` record the nesting decisions. Its stale source list cites pre-`Containers/` paths. |
| **46** | Harden `AbstractProvider<T>` | — | Mechanism done at `Utility@3010155`. **Closes in #119** (per the epic); no separate pass needed. |
| **70** | Legacy `TODO.cs` backlog | — | **Parking lot.** Pull the item-comparison bug out as its own ticket when polish comes up. |

### Frontier (recommended order)

1. **#107** — the only open ticket with all blockers closed on the epic wire. Carry the spec onto the implementing branch first.
2. **#108** — unlocks the rest.
3. **#109 / #110** — parallel; then #111, **#112**, **#113**, **#117**, **#114**, then #115 / #116 / #118, then **#119**.
4. Off the wire, any time: **#122** (triage), **#69** (`/to-spec`). **#63** returns to the frontier once #116 closes.

### Notes from the scan

- **The Services spec is not on `main`.** #106 cites `dev/specs/2026-10-01-services-over-providers-design.md` "on branch `refactor/providers-off-monobehaviour`", but that branch exists neither locally nor on `origin` — it is probably unpushed on the other machine. Push it (or recover the spec) before `/implement #107`.
- #58, #121, #68, #94, #103 are closed and merged; #122 is the only follow-up they left.
- Keep `docs/agents/` and `docs/adr/` out of any `main` → `GitPage` merge (see `CLAUDE.md`). `docs/_config.yml`'s `exclude: [agents, adr]` is unchanged by #87 — the new ADR lands inside the already-excluded `adr/` directory.
