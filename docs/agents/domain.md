# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

**Layout: single-context.** One `CONTEXT.md` at the repo root plus `docs/adr/`.

## Before exploring, read these

- **`CONTEXT.md`** at the repo root — the domain glossary.
- **`docs/adr/`** — read ADRs that touch the area you're about to work in.
- **`dev/specs/`** — this repo's design specs (`YYYY-MM-DD-<topic>-design.md`),
  repo-specific and the richest source of intent for in-flight work. Read the newest
  spec touching your area before proposing changes.
- **`dev/plans/`** — implementation-plan documents written before the 2026-09-01
  workflow switch. Historical, but still the ground truth for anything not yet built
  (e.g. the foundational-rework Phase 0 plan). New implementation work is tracked as
  GitHub Issues via `/to-tickets`, not as files here.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## File structure

This is a Unity project, so source lives under `Assets/`, not `src/`:

```
/
├── CONTEXT.md                     ← domain glossary (created lazily)
├── docs/
│   ├── adr/                       ← architecture decision records (created lazily)
│   └── agents/                    ← this config; see the note in CLAUDE.md
├── dev/
│   ├── specs/                     ← design specs
│   └── plans/                     ← implementation plans
└── Assets/                        ← Unity source, scenes, prefabs, ScriptableObjects
```

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in `CONTEXT.md`. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal — either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding:

> _Contradicts ADR-0007 (event-sourced orders) — but worth reopening because…_
