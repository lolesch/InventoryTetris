# Changelog

Date: 2026-10-08
Status: Scoping spec - not a plan. Slice into a GitHub epic with `/to-tickets`, build with
`/implement`.
Base: `main` at `25c88df8`.
Derived with `/grill-with-docs`. The terms **Changelog**, **Patch Notes**, **Known Issue**,
**Roadmap** and **Icebox** are in `GLOSSARY.md` ("Changelog").

## Problem Statement

A player who finishes a build of the prototype cannot tell what changed since the last one, what is
known to be broken, or what is coming. Known issues live in a code comment (`TODO.cs`), ideas in a
private backlog (`dev/Icebox.md`, which mixes player-safe items with internal design thinking), and
the version number is not even visible on the main menu. A reviewer of the portfolio sees a game with
no visible history. A player who hits one of the two comparison bugs cannot learn that it is known,
and a bug report cannot say which build it came from.

## Solution

The main menu shows the build version as plain text and has a **Changelog** button beside Credits.
The button opens a panel with three tabs: **Patch Notes** (newest version first, the latest one
expanded), **Known Issues** and **Roadmap**. All three come from one hand-written, player-language
Markdown file that ships in the build, so the history is curated and lives in the repo. Fixing a bug
or shipping a feature is a move from a live list into Patch Notes, made in the same change.

## User Stories

1. As a player, I want to see the build version on the main menu, so that I can name my build in a
   bug report.
2. As a player, I want a Changelog button on the main menu, so that I can find out what changed
   without leaving the game.
3. As a player, I want the Changelog to open on Patch Notes, so that the most common question - what
   is new - is answered first.
4. As a player, I want Patch Notes listed newest first, so that I read the latest changes first.
5. As a player, I want the latest Patch Notes entry expanded and older ones collapsed, so that the
   page is short but the history is one click away.
6. As a player, I want each Patch Notes entry to show its version and date, so that I can match it to
   my build.
7. As a player, I want a Known Issues tab, so that I can tell whether what I hit is already known.
8. As a player, I want an empty Known Issues list to say so ("No known issues."), so that a blank
   tab does not look broken.
9. As a player, I want a Roadmap tab, so that I can see what features are intended.
10. As a player, I want an empty Roadmap to say so, so that a blank tab does not look broken.
11. As a player, I want the notes in plain language without ticket numbers or internal terms, so that
    I can read them without knowing how the game is built.
12. As a player, I want switching between the three tabs to be one click and to show exactly one tab
    at a time, so that the panel is never ambiguous.
13. As a player, I want to close the Changelog and return to the main menu, so that I can start a run.
14. As a player, I want the Changelog to never show unreleased work, so that I only read about
    builds I can actually have.
15. As the developer, I want to see unreleased work in the Editor and in development builds, so that
    I can check the wording before a version bump.
16. As the developer, I want to write the Changelog as a Markdown file, so that it is readable and
    diffable on GitHub as well as in the game.
17. As the developer, I want a malformed file to fail a test, so that a typo cannot ship a broken
    panel.
18. As the developer, I want an unsupported construct (bold, links, tables, nested lists) to be
    rejected by that test, so that the file never contains something the game would silently drop.
19. As the developer, I want duplicate or out-of-order versions to fail the test, so that the history
    stays trustworthy.
20. As the developer, I want a change a player would notice to add a bullet under `Unreleased`, so
    that the version bump is a rename and not a rewrite.
21. As the developer, I want fixing a Known Issue or shipping a Roadmap item to remove it from its
    list in the same change, so that no list claims something that is no longer true.
22. As the developer, I want the version bump to rename `Unreleased` to the version and date, so that
    releasing is one edit.
23. As the developer, I want the private Icebox to stay separate, so that half-formed ideas are never
    shown to players by accident.
24. As the developer, I want moving an idea to the Roadmap to mean rewording it for players, so that
    the Roadmap never leaks internal vocabulary.
25. As the developer, I want an entry's version to be the part of the build version before the `+`,
    so that Patch Notes and the main-menu label agree.
26. As a portfolio reviewer, I want a dated, backfilled history of the project's builds, so that I can
    see the project progressing over time.

## Implementation Decisions

- **One feature, one shape.** The **Changelog** is a single document with three sections. Patch Notes
  entries are versioned and append-only; Known Issues and the Roadmap are living lists. A Known Issue
  fixed or a Roadmap item shipped moves into a Patch Notes entry; nothing else moves between sections.
- **Source of truth.** One player-facing Markdown file under `Assets/` (a path Unity ships as a
  TextAsset), referenced by a serialized field on the panel so it needs no Resources or StreamingAssets
  lookup and works in WebGL. The private `dev/Icebox.md` stays the internal idea backlog; its empty
  "Bugs" and "Known Issues" headings are deleted. Promoting an Icebox idea to the Roadmap moves it
  across and rewords it for players.
- **File grammar - headings and bullets only.**
  - `# Patch Notes`, `# Known Issues`, `# Roadmap` open the three sections, each exactly once.
  - Under Patch Notes, each entry opens with `## <version> - <date>` for a build with a version,
    `## <date>` for one that predates version numbers, or `## Unreleased` for the next build.
  - Every item is a `-` bullet, one line each.
  - Everything else (bold, links, nested lists, tables, blank-heading oddities) is a parse error, not
    silently ignored.
- **Versions.** An entry's version is the build version's part before `+` (`0.0.5_Alpha`, not
  `0.0.5_Alpha+1b3c7aa`). Versions are unique and listed newest first.
- **`Unreleased`.** The newest Patch Notes entry may be `Unreleased`. It is parsed always but rendered
  only in the Editor and in development builds; a release build skips it.
- **Seed content.**
  - Patch Notes are backfilled in player language for the three dated builds on record: 2022-01-23,
    2023-10-15 and 0.0.5_Alpha (2026-09-25). The first two are labelled with their dates because no
    version number was ever recorded for them. The draft is made from `git log` and edited by the
    developer. Engineering detail stays in `dev/specs/`, `docs/adr/` and git history.
  - Known Issues: the two comparison bugs recorded in `TODO.cs` (wrong numbers on the unequipped
    item; no comparison against both ring slots or both weapon slots, including two-handed weapons).
  - Roadmap: Pause Menu, Loot Filter and Item Design, reworded for players.
- **Module.** A new assembly, `InventorySystem.Changelog`, holds the parser, the data model and the
  panel's view logic, so it is unit-testable (the scene's panels stay in `Assembly-CSharp` and
  delegate to it; ADR-0007). It extracts to `Utility` only if a second project needs it
  (ADR-0011 puts generic primitives there; this one is not generic yet).
- **Parser interface.** A pure function from the file text to a `Changelog` value with three sections,
  or a parse error that names the line. The model carries Patch Notes entries (version or date label,
  optional date, ordered bullets), and two ordered bullet lists. No Unity types.
- **UI.**
  - The main menu gains a `BundleVersionView` showing the version as plain text, not a button, and a
    Changelog button beside Credits.
  - The button opens a Changelog panel. The panel has three tabs built from the existing tab and
    panel-switch components (an exclusive group of toggles driving three panels) and the same look as
    the left side panels. It opens on Patch Notes.
  - The Changelog panel is **not** a member of the **Inventory Context** enum and is not a **Side
    Panel**: the context decides where Quick Move lands, and a text panel is not a destination
    (ADR-0013). It reuses the components, not the object.
  - Patch Notes shows one collapsible group per entry, the newest expanded. Empty lists show a one-line
    placeholder.
  - The same panel prefab could later be placed in the game scene; that is out of scope here.
- **Workflow (stated in `CLAUDE.md`).** A change a player would notice adds a bullet under
  `Unreleased`; fixing a Known Issue or shipping a Roadmap item removes it from its list in the same
  change; the version bump renames `Unreleased` to the version and date. Checking that the current
  build version has an entry belongs to the release checklist, not to a test.

## Testing Decisions

- **One seam: the parser.** Text in, a `Changelog` or a located parse error out. It is the highest
  point that holds all the behavior worth pinning (grammar, ordering, uniqueness, the `Unreleased`
  rule); the view is a thin binding of the model onto existing, already-tested tab components and is
  verified by opening the main menu once.
- **A good test asserts external behavior only**: given this text, these sections, entries and bullets,
  or this error on this line - never the parser's internal state or helper names.
- **Parser tests (EditMode)**: a well-formed document; each section missing, duplicated or out of
  place; an entry heading with a version and date, with a date only, and `Unreleased`; `Unreleased` not
  first; duplicate and ascending versions rejected; every unsupported construct rejected (bold, links,
  nested bullets, tables, text outside a bullet); empty sections allowed; a trailing newline and CRLF
  line endings accepted.
- **The shipped file is a test**: one EditMode test loads the real file and must parse, so a bad edit
  fails CI (user stories 17-19). It does not require an entry for the current build version.
- **Display rule**: the choice "show `Unreleased`" is a pure function of a development flag over the
  model, tested without Unity.
- **Prior art**: the EditMode tests of the other Unity-free modules (Probability, Data, Geometry),
  each in its own test assembly referencing its module.

## Out of Scope

- Inline Markdown (bold, links, images), nested lists and tables.
- Fetching the Changelog from GitHub or any network at runtime.
- Generating Patch Notes from commits or issues.
- Showing the Changelog in the game scene, or any link from the Known Issues tab to Report a Bug.
- A "what's new since you last played" prompt or an unread marker.
- Localization of the file.
- An enforced release gate (test or CI check) that the current version has an entry.
- Reworking the Icebox beyond removing its two empty headings.

## Further Notes

- `MainMenu.unity` currently has buttons for Start Run, Settings, Credits, Our Discord, Report a Bug
  and Exit, and no version label; `BundleVersionView` has a latent editor-only call to check when it
  is first placed in a built scene.
- No ADR: the shipped-file-versus-Icebox split is cheap to reverse.
- The Settings menu spec (`2026-10-07`) leaves its own entry point on the main menu undecided; the two
  buttons should be laid out together when the second one lands.
