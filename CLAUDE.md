# InventoryTetris

Unity inventory/loot prototype. Source lives under `Assets/`; there is no `src/`.

## Specs and tickets

Design specs go in `dev/specs/YYYY-MM-DD-<topic>-design.md`, committed with a `docs:`
prefix and a body paragraph summarizing the decision. If `/to-spec` — or any skill —
defaults to writing the spec somewhere else (an issue body, a `docs/` subfolder), put
it in `dev/specs/` instead.

Implementation work is broken out of a spec with `/to-tickets` into GitHub Issues, then built one issue at a time with `/implement`, closed with `/code-review`. Execute inline, never use subagents. Before starting `/implement #N`, walk that issue's **Blocked by** chain transitively — `ready-for-agent` means the spec is written, not that the dependencies are closed. If `#N` isn't the frontier, surface that and stop. If the epic swaps a mechanism rather than just adding one, settle what replaces it with `/rederive` before the spec, and run `/drift-review` twice — over the existing slice once the swap is named, and over the new ticket slice before `/implement`. It catches a ticket fixing the mechanism a sibling ticket is about to replace (a stranded fix). **There is no per-phase implementation-plan document** — the issue is the unit of work; if one does not fit a single context window, split it into more issues rather than write a plan. Closing an issue-epic should surface implementation gaps against its spec, or delete it if the spec is fully covered. **The tracker is updated before the PR, on the feature branch, and pushed with it:** after `/code-review`, edit the Frontiers section of `docs/agents/issue-tracker.md` for the issue you just built (drop its row, unblock what it gated, re-scan the frontier) and commit that as a `docs:` commit on the same branch, so the PR carries it and merging the PR is the whole closing step. No separate `docs/tracker-close-N` PR. 

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

`docs/agents/codebase-notes.md` holds durable engineering gotchas that aren't in the code
or git history — Unity compile/test verification (`dotnet build` lies; drive the
`unity-mcp` bridge), assembly-definition layout (namespace ≠ asmdef), the shared `Utility`
submodule, broken `.cs.meta` files, the scene-save modal, and the CRLF + UTF-8 source that
`sed -i` / `perl -pi` corrupt silently. Read it before any Unity compile verification,
asmdef change, or scripted multi-file edit. It's also the cross-machine channel for that kind of
knowledge — agent memory is per-device and doesn't sync; this file does.

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
A root `GLOSSARY.md` sits outside `docs/` and is never part of the site.
