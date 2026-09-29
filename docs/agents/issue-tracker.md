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

Eight open issues; the frontier — an issue whose blockers are all closed and whose criteria are not already met by the code — starts at **#93 → #94**.

**Keep this table current:** update the open set after every `/implement`, and PR-to-`main`.

### Readiness of the open set

| # | Title | Blockers | State vs. this branch |
|---|---|---|---|
| **93** | Hero resource types compile where the simulation can reach them | none | **Frontier now.** `CharacterStat`/`CharacterResource` still live under `Runtime/` (no asmdef there) → `Assembly-CSharp`. Spec is on `origin/spec/enemy-hp-bar-binding` §2; branch is not ancestor of `main` — carry the spec (and the §1/§3–§6 doc for #94) onto the implementing branch. |
| **94** | Enemy HP bars track the live fight | #93 (open), #60 (closed) | **Do after #93.** Pool/display/prefab exist but `SpawnBar`/`RemoveBar` are uncalled — the binding does not exist. No `EnemySpawned` event in `EncounterSimulation`; `Enemy` keeps a parallel `_health` float. |
| **68** | The shared Stash has no home in the domain model | — | **Done, on `issue/68-stash-session-domain-gap`.** `ddbefb5` + its own ADR-0013 implement the deferral ruling; the branch also merges `feature/sidepanel-hotkey-rework`. `main` already holds **0013** for the announce-to-request inversion, so that branch's ADR renumbers to **0014** on merge — both files move. |
| **58** | Healer button + H hotkey | #56 (closed) | **Done in mechanics.** `HealerAction` refills on genuine entry into `Healer`; hotkey `H` (`104`) authored on `HealerToggle`; TownGroup (`ToggleGroup`) membership holds (#57's ruling). Remaining / unverified: visual-feedback acceptance criterion, and `HealerPanel`'s container roster (no `ContainerRole` — a blank grid). Close after a by-hand pass. |
| **63** | Ground items display | LootFlow epic (satisfied: merged `main`) | **Open slice.** `LootFlow.GroundDrops` + `PlaceOnGround` exist, but no `GroundItemSlotDisplay` panel/prefab and no ground quick-move row. **Refresh the ticket first**: its "when no panel is open, shift-quick-move to the floor" criterion must be restated as a row in #86's context table (no ground row exists; `Hero`/`Healer` still resolve to `None`). |
| **69** | Centralized, rebindable input service | — | **Needs `/to-spec`.** Touch `dev/specs/` first: `2026-09-26-session-ux-research.md` §3 + `2026-09-28-save-serialization-prior-art.md` record the nesting decisions (persisted-keybind override vs. Input System package; does not cover discoverability). Also absorbs note: `InventoryContext` moved to `Containers/`, but the ticket's stale source list for the inline reads still cites old paths. |
| **46** | Harden `AbstractProvider<T>` | — | **Mechanism done at `Utility@3010155`** — the submodule split + `OnValidate` + edit-mode guard. The **process** part (architecture pass over `Runtime/Provider/` → `/to-spec` → `/to-tickets` → `/implement`) is still open; the "author-time failure for children/pre-root" direction bullet is only partially covered. Close only once the pass happens and reconciles with the landed commit. |
| **70** | Legacy `TODO.cs` backlog | — | **Parking lot.** Pull the item-comparison bug out as its own ticket when polish comes up. |

### Frontier (recommended order)

1. **#93** — pure asmdef+namespace move (+ `.meta`), no game-code change. Spec on
   `origin/spec/enemy-hp-bar-binding` §2.
2. **#94** — binds the #60 pool to live enemies; depends on #93.
3. **#68** — merge `issue/68-stash-session-domain-gap` (its ADR renumbers to 0014, since
   `main` already holds 0013) and close.
4. **#58** — by-hand visual-feedback pass, then close.
5. **#63** — refresh ticket/table first, then build the ground-items display.

### Notes from the scan

- **Specs under `origin/spec/enemy-hp-bar-binding`** (`dev/specs/2026-09-25-enemy-hp-bar-binding-design.md`) are **not** merged to `main`; the implementing branch for #93/#94 must carry the spec file.
- Pending branches: `issue/68-stash-session-domain-gap` carries finished #68 work; `origin/NewArtwork` is a non-PR art/asset branch (see `codebase-notes.md`).
- Keep `docs/agents/` and `docs/adr/` out of any `main` → `GitPage` merge (see `CLAUDE.md`). `docs/_config.yml`'s `exclude: [agents, adr]` is unchanged by #87 — the new ADR lands inside the already-excluded `adr/` directory.
