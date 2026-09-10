# Handoff — 2026-09-10

**For:** picking this project up on the **tower** (second machine), or any fresh session
after a two-device sync.

This file is transient session state, not project docs. Delete or overwrite it once the
cleanup below is done.

---

## First things on a fresh clone

```
git clone https://github.com/lolesch/InventoryTetris.git
cd InventoryTetris
git submodule update --init          # Assets/Submodules/Utility does NOT auto-checkout
```

Then read `CLAUDE.md` → `CONTEXT.md` → `docs/agents/codebase-notes.md` before touching
Unity or assembly definitions. Agent memory does **not** travel between machines — the
repo files are the shared channel now.

## What just happened this session

Two-device knowledge audit + sync. Done:

- **Pushed** two branches that existed only on the laptop:
  - `origin/feature/acquisition-entrypoint` — one commit, `#35` (route vendor buy through
    `PickUpItem` for auto-equip). Based on pre-#54/#55 `main`; rebase before PR.
  - `origin/NewArtwork` — the only copy of ~53 MB of imported Asset Store UI art under
    `Assets/Art/` (off an old `main`, no PR; pull selectively, not wholesale).
- **Promoted** the laptop's private agent-memory gotchas into the repo — commit `e9fbf70`
  `docs: promote agent codebase gotchas into docs/agents/codebase-notes.md`. New file
  `docs/agents/codebase-notes.md`; `CLAUDE.md` + `docs/agents/domain.md` updated.
- `main` fast-forwarded to `origin/main` (was 8 behind).

## Pending cleanup (needs a human decision — nothing destructive was done)

1. **`feature/trade-flow` local + `InventoryTetris-trade-30` worktree are superseded.**
   `origin/feature/trade-flow` (`1cfa7b0`) was force-rebuilt onto newer `main` with
   equivalent, review-addressed versions of every local commit, plus `#34`, `#66`, and
   **ADR-0012** (Store → "Supply"). Do **not** push the local branch. Recommended:
   ```
   git worktree remove ../InventoryTetris-trade-30
   git branch -D feature/trade-flow            # then re-track origin if needed
   git branch -D fix/menucontext-not-a-provider   # dead: MenuContext.cs deleted on origin
   ```
2. **`prototype/combat-cluster`** — local pointer; its commits are already contained in a
   remote branch. Deletable (`git branch -D prototype/combat-cluster`).
3. **Four stashes, all obsolete** — verify then `git stash drop`:
   - `stash@{0}` / `stash@{1}` — `ShouldReparentOnInstance()` hook on `MenuContext`, a
     class now deleted on `origin/feature/trade-flow`.
   - `stash@{2}` — two deleted `.meta` files + a `ProjectSettings` tweak; junk.
   - `stash@{3}` — TextMesh Pro shader/font autosave churn from opening the project on
     `GitPage`; junk.
4. **`InventoryTetris-54` worktree** has a dirty submodule pointer (` M
   Assets/Submodules/Utility`) — drift, not intentional work. `git -C
   ../InventoryTetris-54 submodule update Assets/Submodules/Utility`.

## Live worktrees (keep)

| Path | Branch | State |
|---|---|---|
| `InventoryTetris` (primary) | `main` | clean, synced |
| `InventoryTetris-54` | `issue/54-side-panel-context` | tracks origin; dirty submodule (see cleanup #4) |
| `InventoryTetris-acquisition-entrypoint` | `feature/acquisition-entrypoint` | now pushed |
| `InventoryTetris-trade-30` | `feature/trade-flow` | superseded — remove (cleanup #1) |

## Open threads (not this session's job)

- `#34`, `#35` — foundational-rework follow-ups; `#35` has a branch now (`feature/acquisition-entrypoint`).
- `goldRatio` calibration (currency system).
- `BundleVersionView` editor-only call — latent bug noted in the shared-UI epic.
- MVP sim epic `#27` — scene wiring landed on `main` (`da4f45d`); confirm smoke pass, then close.

## Suggested skills for the next session

- **`/resolving-merge-conflicts`** — if you choose to reconcile `feature/trade-flow`
  instead of discarding the local branch.
- **`/code-review`** (built-in, run inline — never via subagent) — before opening a PR for
  `feature/acquisition-entrypoint`.
- **`/implement`** with the **Blocked by** chain walked first — for `#34`/`#35` or the next
  epic frontier.
- **`/domain-modeling`** — only if new terms come up; ADR-0012 already landed on `origin`.
