# InventoryTetris

Unity inventory/loot prototype. Source lives under `Assets/`; there is no `src/`.

## Specs and tickets

Design specs go in `dev/specs/YYYY-MM-DD-<topic>-design.md`, committed with a `docs:`
prefix and a body paragraph summarizing the decision. If `/to-spec` — or any skill —
defaults to writing the spec somewhere else (an issue body, a `docs/` subfolder), put
it in `dev/specs/` instead.

Implementation work flows spec -> `/to-tickets` (GitHub Issues) -> `/implement #N`, one issue at a
time -> `/code-review`.

- Execute inline by default. Starting subagents needs the user's decision each time, including
  `/implement-spec`, which runs implementer and merger subagents: ask before spawning, state what
  would run, and wait for a clear yes. One approval covers that one run, never later ones.
- Before `/implement #N`, run `python dev/frontier.py`. `ready-for-agent` means the spec is
  written, not that the dependencies are closed. If `#N` isn't the frontier, surface that and stop.
  The frontier is derived from each issue's **Blocked by**, so a PR carries no tracker commit.
- An epic that swaps a mechanism rather than adding one: settle the replacement with `/rederive`
  before the spec, then `/drift-review` twice, over the existing slice once the swap is named and
  over the new ticket slice before `/implement`. It catches a stranded fix: a ticket fixing the
  mechanism a sibling ticket is about to replace.
- The issue is the unit of work; there is no per-phase implementation-plan document. An issue too
  big for one context window splits into more issues.
- Closing an issue-epic surfaces implementation gaps against its spec, or deletes the epic if the
  spec is fully covered.

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

### Unity verification

`docs/agents/unity-verification.md` — read before a test run, a `Unity_RunCommand`, a compile check, or
scene authoring on a worktree. `dotnet build` lies; `dev/run-tests.sh [EditMode|PlayMode] [filter]` is
the verdict and shadows the project by itself while the Editor is open. A bridge run waits on
`dev/wait-editmode.sh` (a bare `sleep` is rejected). It also covers the scene-save modal.

### Codebase notes

`docs/agents/codebase-notes.md` — read before editing through a shell or python script (`Edit` keeps
CRLF + UTF-8; `perl -pi` mojibakes it), changing an asmdef, hitting a Play Mode reload surprise or a
`.cs.meta` error, and committing in the `Utility` submodule (`dev/bump-utility.sh` pushes it, then
pins it). To enable the pre-commit hook: `git config core.hooksPath dev/hooks`.

## GitHub Pages: keep `docs/agents/` and `docs/adr/` out of `GitPage`

The site at <https://lolesch.github.io/InventoryTetris/> builds from the `GitPage` branch, path
`/docs`. Leave `docs/agents/` and `docs/adr/` out of a `main` -> `GitPage` merge; `exclude:` in
`docs/_config.yml` (byte-identical on both branches) is the backstop that stops Jekyll publishing them.
