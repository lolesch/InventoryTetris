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

Twenty-seven open issues (scanned 2026-10-02, `main` @ `86ed536`); the frontier — an issue whose blockers are all closed and whose criteria are not already met by the code — is **#107** on the Services wire and **#125** / **#126** on the Sold tab wire. #107 is the only unblocked ticket of the Services epic, so that epic is a single wire until #108; the Sold tab epic (#124) is independent of it except that it gates #112 and #115.

**Keep this table current:** update after every `/implement`, and PR-to-`main` by removing closed issues and re-scan the open set.

### Readiness of the open set

| # | Title | Blockers | State vs. this branch |
|---|---|---|---|
| **107** | Services 1: name the per-hero unit, record the service model | #68 (closed) | **Built on `docs/107-hero-state-and-service-model`; closes on merge.** `CONTEXT.md` gains **Hero State** and splits **Session**; ADR-0015 records the service model and the final names; the spec is carried onto the branch. Honours #68 via ADR-0014 (amended). The event is `HeroStateChanged`, not `SessionChanged` — #114 still says the latter. |
| 108 | Services 2: boot, `GameConfig`, locator, runner | #107 | **Frontier once #107 merges.** |
| 109 | Services 3: item service replaces `ItemProvider` | #108 | Blocked. Parallel with #110. |
| 110 | Services 4: `Hero` as a plain class | #108 | Blocked. Parallel with #109. |
| 111 | Services 5: hero stat panel binds to `Hero` | #110 | Blocked. |
| 112 | Services 6: the Hero State holder | #109, #110, **#131** | Blocked. Waits for the Sold tab contract so the staged basket never enters the Hero State; the Sold container is built instead. |
| 113 | Services 7: simulation service on the Hero State | #109, #112 | Blocked. |
| 117 | Services 11: migrate hero callers, delete `DummyTarget` | #111, #112 | Blocked. (Ahead of #114: #114 waits on it.) |
| 114 | Services 8: replace on load, `SessionChanged`, rebinding | #112, #113, #117 | Blocked. |
| 115 | Services 9: migrate inventory callers, GUI | #114, **#131** | Blocked. Waits for the Sold tab contract so it never migrates the basket's GUI callers. |
| 116 | Services 10: migrate inventory callers, runtime + simulation | #114 (#103 closed) | Blocked. |
| 118 | Services 12: `DebugPanel` replaces UnityEvent buttons | #112, #116 | Blocked. |
| 119 | Services 13: contract, delete the old providers | #115, #116, #117, #113, #118 | Blocked. Closes #46. |
| **106** | Epic: Services over providers | #68 (closed) | Epic. Closes when #119 does. |
| **63** | Ground items display | #116 (new), LootFlow epic (merged) | **Now blocked, no longer the frontier.** The ticket was amended to name the acquisition entry point on the inventory service. Still owes the refresh: its "shift-quick-move to the floor" criterion must become a ground row in #86's context table. |
| **124** | Epic: Sold tab — immediate sale and rebuy | — | Epic. Spec `dev/specs/2026-10-02-sold-tab-design.md`. Closes when #131 does; surface gaps against the spec first. Replaces the staged Sell Basket and supersedes ADR-0012. |
| **125** | Sold tab 1: unaffordable items cannot be picked up | — | **Frontier.** Prefactor; stands alone. |
| **126** | Sold tab 2: the Sale (container seam) | — | **Frontier.** Expand: lands beside the basket. |
| 127 | Sold tab 3: the Sold tab appears in the panels | #126 | Blocked. |
| 128 | Sold tab 4: shift-click sells, buy back like a Supply | #127 | Blocked. |
| 129 | Sold tab 5: dropping on the Supply or Sold tab sells | #128 | Blocked. |
| 130 | Sold tab 6: remove the staged basket from panels and scenes | #128, #129 | Blocked. Scene work through the `unity-mcp` bridge. |
| 131 | Sold tab 7: delete the staged sale code and tests, record the decision | #130 | Blocked. Contract; also gates #112 and #115. |
| **122** | Items with no affixes are worthless | — | **Needs triage** (`needs-triage`). `ItemView.SellValue` sums affix values, so a consumable with none prices at 0: unsellable alone, free on a Supply shelf. Value-model fix, not a basket change. Independent of the epic; its criteria were reworded for #124 (a book *sells*, no basket to confirm). |
| **69** | Centralized, rebindable input service | — | **Needs `/to-spec`.** `2026-09-26-session-ux-research.md` §3 + `2026-09-28-save-serialization-prior-art.md` record the nesting decisions. Its stale source list cites pre-`Containers/` paths. |
| **46** | Harden `AbstractProvider<T>` | — | Mechanism done at `Utility@3010155`. **Closes in #119** (per the epic); no separate pass needed. |
| **70** | Legacy `TODO.cs` backlog | — | **Parking lot.** Pull the item-comparison bug out as its own ticket when polish comes up. |

### Frontier (recommended order)

1. **#107** — built on its branch; merge it to close.
2. **#108** — unlocks the rest.
3. **#109 / #110** — parallel; then #111, **#112**, **#113**, **#117**, **#114**, then #115 / #116 / #118, then **#119**.
4. **The Sold tab wire runs beside it:** **#125** and **#126** now, then #127 → #128 → #129 → #130 → #131. Starting it beside #107–#110 keeps #112 from waiting on it.
5. Off the wire, any time: **#122** (triage), **#69** (`/to-spec`). **#63** returns to the frontier once #116 closes.

### Notes from the scan

- **The Services spec is carried onto the #107 branch** (`dev/specs/2026-10-01-services-over-providers-design.md`, copied from `origin/provider-off-monobehaviour`; #106's branch name `refactor/providers-off-monobehaviour` is wrong). It lands on `main` when #107 merges.
- **The Sold tab spec is on `main`** (`86ed536`, pushed 2026-10-02). It contradicts the Services spec in one place: the Hero State's contents there list a "Sell Basket", which #124 replaces with the Sold container; #112 and #115 were reworded and blocked on #131 for that reason.
- #58, #121, #68, #94, #103 are closed and merged; #122 is the only follow-up they left.
- Keep `docs/agents/` and `docs/adr/` out of any `main` → `GitPage` merge (see `CLAUDE.md`). `docs/_config.yml`'s `exclude: [agents, adr]` is unchanged by #87 — the new ADR lands inside the already-excluded `adr/` directory.
