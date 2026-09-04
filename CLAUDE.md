# InventoryTetris

Unity inventory/loot prototype. Source lives under `Assets/`; there is no `src/`.

## Specs and tickets

Design specs go in `dev/specs/YYYY-MM-DD-<topic>-design.md`, committed with a `docs:`
prefix and a body paragraph summarising the decision. If `/to-spec` — or any skill —
defaults to writing the spec somewhere else (an issue body, a `docs/` subfolder), put
it in `dev/specs/` instead.

Implementation work is broken out of a spec with `/to-tickets` into GitHub Issues
(`lolesch/InventoryTetris`), then built one issue at a time with `/implement` (TDD via
`/tdd`, closed with `/code-review`). Execute inline, never via subagents. **There is no
per-phase implementation-plan document** — the issue is the unit of work; if one does
not fit a single context window, split it into more issues rather than write a plan.

`dev/plans/` holds the plans written before this switch (2026-09-01). They are still
valid to execute as written — the foundational-rework Phase 0 plan
(`2026-08-31-foundational-rework-phase-0.md`) is referenced by issues #3–#4. Don't add
new files there.

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

`docs/agents/` is agent configuration, not site content, and so is the `docs/adr/`
directory `/domain-modeling` will create. Both are excluded from the built site by
`exclude:` in `docs/_config.yml`, which is kept byte-identical on `main` and `GitPage`
so a merge in either direction cannot resolve the protection away.

That exclude is the enforcement; prefer leaving `docs/agents/` and `docs/adr/` out of a
`main` -> `GitPage` merge anyway. Without it these files would be *published*, though
not rendered: Jekyll copies files with no YAML front matter to the destination verbatim,
so they would be fetchable at `/agents/issue-tracker.md` rather than turned into HTML.
A root `CONTEXT.md` sits outside `docs/` and is never part of the site.
