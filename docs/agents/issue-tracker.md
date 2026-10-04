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

Eighteen open issues (scanned 2026-10-04, `main` @ `45b9a03`, then updated for #111 on `feat/111-hero-stat-panel`, for #126 on `feat/126-the-sale`, and for #127 and #128 on `feat/127-sold-tab-panels`, each of which closes with its PR); the frontier — an issue whose blockers are all closed and whose criteria are not already met by the code — is **#129** on the Sold tab wire. #125 is built: `VendorTransaction.CanAffordPickUp` prices the amount a pick-up takes (`Package.PickUpAmount`), `VendorSlotDisplay` refuses an unaffordable grab, and the shelf tint reads `CanAffordBuy`, the gate the drop keeps. #126 is built: `Sale` (`TrySell`, `TrySellHeld`) and `SoldContainer` in `InventorySystem.Containers`, `Wallet.CanDeposit`, and `QuickMoveResolver` takes the Sold container as one more Supply; `InventoryProvider.Sold` holds it, and nothing in the GUI calls the sale yet. #127 is built: `ContainerRole.Sold` is appended after `HealerSupply`, and the Vendor and Healer panels each show a Supply and a Sold tab (two `PanelToggle`s in a no-switch-off `ToggleGroup`), both Sold grids binding the one container; the Sold tab takes the Basket's slot, and the Healer panel is a copy of the Vendor's layout. #128 is built on the same branch: the Vendor and Healer quick-move sink is `QuickMoveIntent.Sell` (`Sale.TrySell`), a Restock clears the Sold container, and `ContainerRoleResolver` holds the role-to-container mapping. Neither panel holds a basket grid, Confirm/Cancel buttons or a `SellBasketDisplay` any more, so #130's panel criteria are met; what is left of it is `OldVendorPanel` (inactive) and the basket display prefab. The staged code (`SellBasket`, `QuickMoveIntent.SellBasket`, `BasketSlotDisplay`) still compiles until #131. The Services wire has no frontier of its own: #112 waits on #131, the end of the Sold tab wire. #109 is merged: `IItemService`/`ItemService` in `InventorySystem.Services`, and `ItemProvider` is gone. #110 is merged: `Hero` and `HeroData` are in `InventorySystem.Characters`, `LocalPlayer` delegates to them. #111 is built: `CharacterStatPanel` binds to `Hero.StatsChanged` and `LocalPlayer` holds no display. #108 is merged: the registry, locator and game loop are in `Utility`, and `GameConfig`, `GameBoot` and `GameLoop` in `InventorySystem.Services` (ADR-0015, amended 2026-10-04). The Sold tab epic (#124) is independent of it except that it gates #112 and #115.

**Keep this table current:** update it at the end of every `/implement` — after `/code-review`, **before the PR is opened** — by removing the issue the branch closes, unblocking what it gated and re-scanning the open set. It is a `docs:` commit on the feature branch itself and is pushed with it, so the PR carries the tracker and merging the PR closes the issue and moves the frontier in one step. There is no separate tracker PR after the merge. When two branches run in parallel (as #109 and #110 did), the second to merge resolves the table conflict by re-scanning, not by picking a side.

### Readiness of the open set

| # | Title | Blockers | State vs. this branch |
|---|---|---|---|
| 112 | Services 6: the Hero and the World, built in explicit order | **#131** (#109, #110 merged) | Blocked. `LocalPlayer.PickUpItem` still reaches the containers through `InventoryProvider`; `Hero` is not an `IItemReceiver` until this builds it in order. Waits for the Sold tab contract so the staged basket never enters the World; the Sold container is built instead. |
| 113 | Services 7: simulation service over the Hero and the World | #112 (#109 merged) | Blocked. |
| 117 | Services 11: migrate hero callers, delete `DummyTarget` | #112 (#111 closes with its PR) | Blocked. (Ahead of #114: #114 waits on it.) |
| 114 | Services 8: replace on load, `HeroLoaded`, rebinding | #112, #113, #117 | Blocked. |
| 115 | Services 9: migrate inventory callers, GUI | #114, **#131** | Blocked. Waits for the Sold tab contract so it never migrates the basket's GUI callers. |
| 116 | Services 10: migrate inventory callers, runtime + simulation | #114 (#103 closed) | Blocked. |
| 118 | Services 12: `DebugPanel` replaces UnityEvent buttons | #112, #116 | Blocked. |
| 119 | Services 13: contract, delete the old providers | #115, #116, #117, #113, #118 | Blocked. Closes #46. |
| **106** | Epic: Services over providers | #68 (closed) | Epic. Closes when #119 does. |
| **63** | Ground items display | #116 (new), LootFlow epic (merged) | **Now blocked, no longer the frontier.** The ticket was amended to name the acquisition entry point on the inventory service. Still owes the refresh: its "shift-quick-move to the floor" criterion must become a ground row in #86's context table. |
| **124** | Epic: Sold tab — immediate sale and rebuy | — | Epic. Spec `dev/specs/2026-10-02-sold-tab-design.md`. Closes when #131 does; surface gaps against the spec first. Replaces the staged Sell Basket and supersedes ADR-0012. |
| **129** | Sold tab 5: dropping on the Supply or Sold tab sells | — (#128 built) | **Frontier.** `Sale.TrySellHeld` exists; no drop target calls it. |
| 130 | Sold tab 6: remove the staged basket from panels and scenes | #129 (#128 built) | Blocked on #129. Its panel criteria are already met (see above); `OldVendorPanel` and the basket display prefab remain. Scene work through the `unity-mcp` bridge. |
| 131 | Sold tab 7: delete the staged sale code and tests, record the decision | #130 | Blocked. Contract; also gates #112 and #115. |
| **122** | Items with no affixes are worthless | — | **Needs triage** (`needs-triage`). `ItemView.SellValue` sums affix values, so a consumable with none prices at 0: unsellable alone, free on a Supply shelf. Value-model fix, not a basket change. Independent of the epic; its criteria were reworded for #124 (a book *sells*, no basket to confirm). |
| **69** | Centralized, rebindable input service | — | **Needs `/to-spec`.** `2026-09-26-session-ux-research.md` §3 + `2026-09-28-save-serialization-prior-art.md` record the nesting decisions. Its stale source list cites pre-`Containers/` paths. |
| **46** | Harden `AbstractProvider<T>` | — | Mechanism done at `Utility@3010155`. **Closes in #119** (per the epic); no separate pass needed. |
| **70** | Legacy `TODO.cs` backlog | — | **Parking lot.** Pull the item-comparison bug out as its own ticket when polish comes up. |

### Frontier (recommended order)

1. **#112** once #131 closes (the Sold tab wire); then **#113**, **#117**, **#114**, then #115 / #116 / #118, then **#119**.
2. **The Sold tab wire runs beside it:** **#129** now (#125–#128 are built), then #130 → #131. #112 waits on #131, so this wire is now the long pole for the Services epic.
3. Off the wire, any time: **#122** (triage), **#69** (`/to-spec`). **#63** returns to the frontier once #116 closes.

### Notes from the scan

- **The Services spec is on `main`** (`dev/specs/2026-10-01-services-over-providers-design.md`, carried unchanged from `origin/provider-off-monobehaviour` by #107's PR #132). **It still says "Hero State" and `SessionChanged`.** ADR-0015 supersedes that vocabulary: read it as the **Hero** and the **World**, and `HeroLoaded`. The spec's own Hero State contents list also says "Sell Basket", read as the Sold container (the next note). Issues #106, #112–#117, #124 and #131 were reworded to match on 2026-10-03.
- **The Sold tab spec is on `main`** (`86ed536`, pushed 2026-10-02). It contradicts the Services spec in one place: the Hero State's contents there list a "Sell Basket", which #124 replaces with the Sold container; #112 and #115 were reworded and blocked on #131 for that reason.
- #58, #121, #68, #94, #103 are closed and merged; #122 is the only follow-up they left.
- Keep `docs/agents/` and `docs/adr/` out of any `main` → `GitPage` merge (see `CLAUDE.md`). `docs/_config.yml`'s `exclude: [agents, adr]` is unchanged by #87 — the new ADR lands inside the already-excluded `adr/` directory.
