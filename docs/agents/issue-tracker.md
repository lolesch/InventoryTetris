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

Fourteen open issues (scanned 2026-10-04, `main` @ `45b9a03`, then updated for #111 on `feat/111-hero-stat-panel`, for #126 on `feat/126-the-sale`, for #127, #128 and #129 on `feat/127-sold-tab-panels`, for #130 and #131 on `feat/130-remove-staged-basket`, for #112 on `feat/112-hero-and-world`, each of which closes with its PR); the frontier — an issue whose blockers are all closed and whose criteria are not already met by the code — is **#113** on the Services wire. #125 is built: `VendorTransaction.CanAffordPickUp` prices the amount a pick-up takes (`Package.PickUpAmount`), `VendorSlotDisplay` refuses an unaffordable grab, and the shelf tint reads `CanAffordBuy`, the gate the drop keeps. #126 is built: `Sale` (`TrySell`, `TrySellHeld`) and `SoldContainer` in `InventorySystem.Containers`, `Wallet.CanDeposit`, and `QuickMoveResolver` takes the Sold container as one more Supply; `InventoryProvider.Sold` holds it, and nothing in the GUI calls the sale yet. #127 is built: `ContainerRole.Sold` is appended after `HealerSupply`, and the Vendor and Healer panels each show a Supply and a Sold tab (two `PanelToggle`s in a no-switch-off `ToggleGroup`), both Sold grids binding the one container; the Sold tab takes the Basket's slot, and the Healer panel is a copy of the Vendor's layout. #128 is built on the same branch: the Vendor and Healer quick-move sink is `QuickMoveIntent.Sell` (`Sale.TrySell`), a Restock clears the Sold container, and `ContainerRoleResolver` holds the role-to-container mapping. Neither panel holds a basket grid, Confirm/Cancel buttons or a `SellBasketDisplay` any more, so #130's panel criteria were met. #130 is built: `OldVendorPanel` and `SlotDisplayBasket.prefab` are gone, and `VendorToggle` (hotkey V) opens the new `VendorPanel`, which no toggle opened before. #131 is built on the same branch: the staged code is deleted (`SellBasket`, `SellBasketQuickMove`, `SellBasketDisplay`, `BasketSlotDisplay`, `QuickMoveIntentKind.SellBasket`, the provider's `Basket` and `basketSize`, their tests), `ContainerRole.Basket` is deleted with every display re-serialized (`HealerSupply` 5, `Sold` 6), and ADR-0016 supersedes ADR-0012. #129 is built on the same branch: a Supply slot (the Sold tab's slots are the same class) is a drop target that sells through `Sale.TrySellHeld`, asks `Sale.CanSellHeld` first so the forbidden tint shows, and returns a purchase in progress (`DragProvider.IsHoldingPurchase`) to its origin free. The Sold tab wire is done; the Services wire resumed at #112. #112 is built: `SessionBuilder` builds the Hero (stats, then Equipment, Inventory, Stash, Wallet and the Behaviour Profile through `Hero.Outfit`) and then the `World` (both Supplies, the Sold container, the Inventory Context); `GameBoot` builds it from `GameConfig.DefaultHero`, registers `ISession` and `IInventoryService` (containers by role, Quick Move, acquisition with the debug Stash overflow, Restock, which still clears Sold), and stocks both Supplies. `InventoryProvider`, `CharacterProvider` and `SimulationProvider.Behaviour` forward to them, `LocalPlayer` wraps the booted Hero and holds no `HeroData`, and ADR-0015 records the grouping. #109 is merged: `IItemService`/`ItemService` in `InventorySystem.Services`, and `ItemProvider` is gone. #110 is merged: `Hero` and `HeroData` are in `InventorySystem.Characters`, `LocalPlayer` delegates to them. #111 is built: `CharacterStatPanel` binds to `Hero.StatsChanged` and `LocalPlayer` holds no display. #108 is merged: the registry, locator and game loop are in `Utility`, and `GameConfig`, `GameBoot` and `GameLoop` in `InventorySystem.Services` (ADR-0015, amended 2026-10-04). The Sold tab epic (#124) is independent of it except that it gates #112 and #115.

**Keep this table current:** update it at the end of every `/implement` — after `/code-review`, **before the PR is opened** — by removing the issue the branch closes, unblocking what it gated and re-scanning the open set. It is a `docs:` commit on the feature branch itself and is pushed with it, so the PR carries the tracker and merging the PR closes the issue and moves the frontier in one step. There is no separate tracker PR after the merge. When two branches run in parallel (as #109 and #110 did), the second to merge resolves the table conflict by re-scanning, not by picking a side.

### Readiness of the open set

| # | Title | Blockers | State vs. this branch |
|---|---|---|---|
| **113** | Services 7: simulation service over the Hero and the World | — (#109 merged, #112 built) | **Frontier.** `SimulationProvider` still holds the Run, loot flow, Corpse and settlement; its `Behaviour` already reads the Hero's. |
| 117 | Services 11: migrate hero callers, delete `DummyTarget` | — (#112 built, #111 built) | **Frontier** alongside #113. (Ahead of #114: #114 waits on it.) |
| 114 | Services 8: replace on load, `HeroLoaded`, rebinding | #113, #117 (#112 built) | Blocked. |
| 115 | Services 9: migrate inventory callers, GUI | #114 (#131 built) | Blocked. |
| 116 | Services 10: migrate inventory callers, runtime + simulation | #114 (#103 closed) | Blocked. |
| 118 | Services 12: `DebugPanel` replaces UnityEvent buttons | #116 (#112 built) | Blocked. |
| 119 | Services 13: contract, delete the old providers | #115, #116, #117, #113, #118 | Blocked. Closes #46. |
| **106** | Epic: Services over providers | #68 (closed) | Epic. Closes when #119 does. |
| **63** | Ground items display | #116 (new), LootFlow epic (merged) | **Now blocked, no longer the frontier.** The ticket was amended to name the acquisition entry point on the inventory service. Still owes the refresh: its "shift-quick-move to the floor" criterion must become a ground row in #86's context table. |
| **124** | Epic: Sold tab — immediate sale and rebuy | — | Epic. Spec `dev/specs/2026-10-02-sold-tab-design.md`. **Closes with #131's PR; surface gaps against the spec first** (or delete the epic if it is fully covered). Replaces the staged Sell Basket and supersedes ADR-0012. |
| **122** | Items with no affixes are worthless | — | **Needs triage** (`needs-triage`). `ItemView.SellValue` sums affix values, so a consumable with none prices at 0: unsellable alone, free on a Supply shelf. Value-model fix, not a basket change. Independent of the epic; its criteria were reworded for #124 (a book *sells*, no basket to confirm). |
| **69** | Centralized, rebindable input service | — | **Needs `/to-spec`.** `2026-09-26-session-ux-research.md` §3 + `2026-09-28-save-serialization-prior-art.md` record the nesting decisions. Its stale source list cites pre-`Containers/` paths. |
| **46** | Harden `AbstractProvider<T>` | — | Mechanism done at `Utility@3010155`. **Closes in #119** (per the epic); no separate pass needed. |
| **70** | Legacy `TODO.cs` backlog | — | **Parking lot.** Pull the item-comparison bug out as its own ticket when polish comes up. |

### Frontier (recommended order)

1. **#113** and **#117** now (#112 is built); then **#114**, then #115 / #116 / #118, then **#119**.
2. Off the wire, any time: **#122** (triage), **#69** (`/to-spec`). **#63** returns to the frontier once #116 closes.

### Notes from the scan

- **The Services spec is on `main`** (`dev/specs/2026-10-01-services-over-providers-design.md`, carried unchanged from `origin/provider-off-monobehaviour` by #107's PR #132). **It still says "Hero State" and `SessionChanged`.** ADR-0015 supersedes that vocabulary: read it as the **Hero** and the **World**, and `HeroLoaded`. The spec's own Hero State contents list also says "Sell Basket", read as the Sold container (the next note). Issues #106, #112–#117, #124 and #131 were reworded to match on 2026-10-03.
- **The Sold tab spec is on `main`** (`86ed536`, pushed 2026-10-02). It contradicts the Services spec in one place: the Hero State's contents there list a "Sell Basket", which #124 replaces with the Sold container; #112 and #115 were reworded and blocked on #131 for that reason.
- #58, #121, #68, #94, #103 are closed and merged; #122 is the only follow-up they left.
- Keep `docs/agents/` and `docs/adr/` out of any `main` → `GitPage` merge (see `CLAUDE.md`). `docs/_config.yml`'s `exclude: [agents, adr]` is unchanged by #87 — the new ADR lands inside the already-excluded `adr/` directory.
