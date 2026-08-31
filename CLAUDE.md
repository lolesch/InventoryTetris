# InventoryTetris

Unity inventory/loot prototype. Source lives under `Assets/`; there is no `src/`.

## Specs and plans

Design specs go in `dev/specs/YYYY-MM-DD-<topic>-design.md`, implementation plans in
`dev/plans/YYYY-MM-DD-<topic>.md`. Commit them with a `docs:` prefix and a body
paragraph summarising the decision. When a skill defaults to writing a spec or plan
somewhere else (e.g. `docs/superpowers/specs/`), write it to `dev/` instead.

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (`lolesch/InventoryTetris`), driven by the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five canonical label names are used verbatim — `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` at the repo root plus `docs/adr/`, both created lazily. See `docs/agents/domain.md`.

## GitHub Pages: do not merge `docs/agents/` into `GitPage`

The published site at <https://lolesch.github.io/InventoryTetris/> is built from the
**`GitPage` branch**, path `/docs` (verified via `gh api repos/lolesch/InventoryTetris/pages`).
Nothing under `docs/` on `main` is published today.

`docs/agents/` is agent configuration, not site content. If `main` is ever merged into
`GitPage`, **exclude `docs/agents/`** — Jekyll would otherwise render those files as
public pages at `/agents/*.html`. `docs/adr/` and a root `CONTEXT.md`, once they exist,
carry the same caveat.
