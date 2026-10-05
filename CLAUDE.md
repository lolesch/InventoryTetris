# InventoryTetris

Unity inventory/loot prototype. Source lives under `Assets/`; there is no `src/`.

## Specs and tickets

Design specs go in `dev/specs/YYYY-MM-DD-<topic>-design.md`, committed with a `docs:`
prefix and a body paragraph summarizing the decision. If `/to-spec` — or any skill —
defaults to writing the spec somewhere else (an issue body, a `docs/` subfolder), put
it in `dev/specs/` instead.

Implementation work is broken out of a spec with `/to-tickets` into GitHub Issues, then built one issue at a time with `/implement`, closed with `/code-review`. Execute inline, never use subagents. Before starting `/implement #N`, run `python dev/frontier.py` — `ready-for-agent` means the spec is written, not that the dependencies are closed. If `#N` isn't the frontier, surface that and stop. The frontier is derived from the issues' **Blocked by**, not recorded in a doc, so a PR carries no tracker commit. If the epic swaps a mechanism rather than just adding one, settle what replaces it with `/rederive` before the spec, and run `/drift-review` twice — over the existing slice once the swap is named, and over the new ticket slice before `/implement`. It catches a ticket fixing the mechanism a sibling ticket is about to replace (a stranded fix). **There is no per-phase implementation-plan document** — the issue is the unit of work; if one does not fit a single context window, split it into more issues rather than write a plan. Closing an issue-epic should surface implementation gaps against its spec, or delete it if the spec is fully covered.

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (`lolesch/InventoryTetris`), driven by the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` at the repo root plus `docs/adr/`, both created lazily. See `docs/agents/domain.md`.

### Coding conventions

Read before writing a script, or during `/code-review`/`/simplify`.
`docs/agents/coding-conventions.md`.

### Codebase notes

`docs/agents/codebase-notes.md` — read before Unity compile or test verification (`dotnet build`
lies; drive the `unity-mcp` bridge), an asmdef change, or a scripted multi-file edit (`perl -pi`
mojibakes the UTF-8 source), and to enable the pre-commit hook (`git config core.hooksPath dev/hooks`). It also covers broken `.cs.meta` files, the scene-save
modal and the `Utility` submodule.

## GitHub Pages: keep `docs/agents/` and `docs/adr/` out of `GitPage`

The site at <https://lolesch.github.io/InventoryTetris/> builds from the `GitPage` branch, path
`/docs`. Leave `docs/agents/` and `docs/adr/` out of a `main` -> `GitPage` merge; `exclude:` in
`docs/_config.yml` (byte-identical on both branches) is the backstop that stops Jekyll publishing them.
