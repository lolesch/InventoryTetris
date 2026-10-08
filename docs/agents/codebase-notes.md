# Codebase Notes

Durable, hard-won facts about how *this* repo behaves that you can't get from the code, git history, or CLAUDE.md. Read before touching assembly definitions or scripting a multi-file edit, and when a build error doesn't match your diff. Compile and test verification is in `unity-verification.md` (headless runs, worktree batch mode, batch Play Mode) and `unity-bridge.md` (the `unity-mcp` bridge, Play Mode by hand).

This file is the shared channel for that knowledge across machines — an agent's private memory does not travel between devices, this does. Keep it current; prune what stops being true.

---

## Enter Play Mode Settings — domain/scene reload disabled

`ProjectSettings/EditorSettings.asset` has both `DisableDomainReload` and `DisableSceneReload` on
(`m_EnterPlayModeOptions: 3`). Two consequences:

- **A scene object survives a Play session.** Exiting Play calls `OnDisable`; re-entering does
  **not** call `Awake` again, only `OnEnable`. A component that subscribes in `Awake` and
  unsubscribes in `OnDisable` is permanently unsubscribed from the second Play entry on, silently,
  while a sibling that subscribes in `OnEnable` keeps working, so the system looks half-alive (in
  #85: "the panels moved but the toggle stayed pressed"). Subscribe in `OnEnable`, release in
  `OnDisable`. Use `override` when the base chain owns `OnEnable` (`Selectable` does); a same-named
  method hides the base's instead of running beside it.
- **Statics survive Stop.** `AbstractSceneSingleton<T>._isQuitting` is set by `OnApplicationQuit`
  and never reset, so the next Play entry would return `null` from every provider's `Instance`.
  `Utility/Editor/ProviderQuittingResetGuard.cs` resets it on `ExitingEditMode`. It walks
  `AbstractSceneSingleton<T>` because that declares the field: `GetField` never sees a private
  base-class field through a derived type, so a guard that names the wrong type silently stops
  working (it did, 2026-09-21, after the class split). After any refactor that moves a field between
  base classes, grep for the guarded field's name; nothing fails to compile. No test covers the
  guard. For new statics see the `SubsystemRegistration` bullet under Assembly definitions.

## `CS0103` / `CS0246` that is really a broken `.meta`

Script metas here come in three shapes, and only one of them breaks anything. Name them,
because the two healthy ones keep getting re-reported as bugs:

| Shape | On disk | Unity's verdict |
|---|---|---|
| **full** | 11 lines, ~254 B, ends `assetBundleVariant: ` + newline | fine |
| **short** | 59–62 B (or ~85 B with `timeCreated:`) — `fileFormatVersion` + `guid` and no `MonoImporter:` block | fine; Unity rewrites the full form on its next import |
| **cut** | opens the `MonoImporter:` block but stops mid-line, ~239 B, no trailing newline | rejected |

A **cut** meta fails YAML parsing (`Parser Failure at line 11: Expect ':' ...`), Unity logs
"does not have a valid GUID and its corresponding Asset file will be ignored", the `.cs`
never compiles, and every consumer fails `CS0103: The name 'X' does not exist`. It **looks
like a missing-assembly / asmdef-reference problem and is not.** A **warm Library masks
it** — a session that already imported good metas runs the whole suite green with cut metas
still on disk. A fresh checkout / Library wipe breaks.

**Confirm the shape before touching a meta.** `AssetDatabase.AssetPathToGUID(path)` is the
authority: `""` means Unity ignored it (**cut**), any other value means it resolved
(**full** or **short**). On disk the signature of a **cut** meta is that it starts the
importer block and never finishes it: `MonoImporter` present, last byte not a newline. The
pre-commit hook (`dev/hooks/pre-commit`) runs that check on every staged `*.cs.meta`.

**`grep -L assetBundleVariant` finds the opposite set.** The cut lands *at* that line, so a
**cut** meta still contains the string and the grep skips it; what it returns is every
**short** meta, all of them healthy. A scan built on it hands you a long, confident,
entirely wrong worklist — that is exactly how #80 got filed.

Measured 2026-09-19 (#80): 91 of 282 metas were **short**, none **cut**; EditMode on a copy with no
`Library/` passed 762/762 before and after normalising them, so leave **short** metas alone.

To repair a genuinely **cut** meta: rewrite it canonically (`fileFormatVersion: 2` …
`assetBundleVariant: ` + trailing newline), **preserving the committed GUID** — grep
`Assets/Scenes/*.unity` for that GUID first; scene `m_Script` refs break if it changes.
Then `AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)` +
`CompilationPipeline.RequestScriptCompilation()` through the bridge.

## Scripted edits from git-bash

**Reach for `Edit` first, for one file or fifty.** It keeps CRLF and UTF-8 (next section), needs no
quoting, and a repeated `old_string`/`new_string` pair is cheaper than a script that re-implements the
line-ending handling (eight such scripts died on a stray quote in the transcripts of 10/5-10/7).
`Edit` and `Write` refuse a file you have not `Read` in the current context, and a compaction empties
that record: after one, re-`Read` the file before editing instead of falling back to `python`.

If a script is genuinely the right tool (a pattern across many files), it runs under git-bash on
Windows, so a `python` or `.exe` call sees Windows paths and the shell sees MSYS ones:

- **`/tmp` is not the same folder to both.** A script written to `/tmp/x.py` and run by `python` fails
  to import its neighbours. Write throwaway scripts to the session's scratchpad directory and run them
  from there.
- **Inline `python - <<'EOF'` heredocs break on a stray backtick or quote in the body** (one killed a
  command with `Permission denied`). Past a few lines, write the script to a file and run it.
- **Anything that rewrites many files must keep each file's line endings** (CRLF and LF both exist in
  the working tree). Read with `newline=''`, detect `\r\n`, and convert the replacement strings.

## Source is CRLF + UTF-8 — stream editors can still corrupt it

Source under `Assets/Scripts/` and the docs are **CRLF in the working tree, LF in the index**
(`.gitattributes`: `*.cs` and `*.md` are `text eol=crlf`, so this holds whatever `core.autocrlf`
says). The docstrings are dense with em dashes (—) and other non-ASCII. Because git normalises
on add, a stream editor's line-ending flip no longer shows up as churn. The encoding damage does:

- **`perl -0pi -e` mojibakes existing UTF-8** (— becomes â) as soon as the replacement
  string itself contains a wide character — it switches to character semantics on output
  only. The pre-commit hook rejects mojibake in added lines of `*.cs` and `*.md`.
- **`sed -i`** rewrites with LF; harmless to git now, but it still changes the working copy that
  Unity and the editors read.

Neither failure shows up in a test run: the code still compiles and the suite still passes.

**Use an editor tool that preserves encoding and endings** (Claude Code's `Edit`) for
anything touching these files, even mechanical multi-file renames. If a stream editor is
genuinely the right tool, restrict the glob to files that will actually match and check
`git diff` afterwards.

## Assembly definitions — namespace ≠ asmdef

**Folder path decides which assembly a file compiles into; the namespace does not.**
Re-derive the assembly graph folder-by-folder (the `*.asmdef` files) rather than trusting
namespace names when a reference won't resolve.

- **`Assembly-CSharp` has zero test coverage.** Code that needs unit tests lives in its own
  asmdef. A custom asmdef **cannot reference `Assembly-CSharp`**, so whatever one needs must sit in
  an asmdef too: `Data/Distributions/` is `InventorySystem.Distributions` (it resolves from
  `Assembly-CSharp` only because it is `autoReferenced`; `InventorySystem.Services` names it
  explicitly), and `ItemTypeData` moved to `Data/Statistics/` for `GameConfig`'s sake (move the
  `.meta` with the file so the GUID and scene references hold). The scene's
  panels and displays stay in `Assembly-CSharp` and delegate to asmdef types; tests reach them by type name.
- **`Simulation` and `Locations` are Unity-free by convention, not by flag.** `noEngineReferences`
  is `false`, but files get **no** implicit `using`: a file there that needs `Mathf` must
  `using UnityEngine;`, one that needs `System.Math` must `using System;`. Check every new file.
- **`GameConfig`** (`Assets/Resources/GameConfig.asset`) uses `[field: SerializeField]`
  auto-properties, so a hand-written `.asset` or a test's `SerializedObject` uses
  `<Name>k__BackingField` keys (`TestGameConfig` in the Services tests).
- **Editing a scene through the open Editor saves the Editor's memory, not the file.** If the
  working copy of `Example.unity` differs from what the Editor loaded (a branch switch, a hand
  edit), a `RunCommand` that deletes objects and saves also rewrites every other difference
  (1.8k diff lines on #117). `EditorSceneManager.OpenScene` the scene first so memory equals disk,
  then edit and `SaveOpenScenes`; check `diff` against a copy taken before.
- **Static state needs a `SubsystemRegistration` reset** (`ServiceLocator`, `GameLoop` have one)
  **and a clear on `EnteredEditMode`**, not on `ExitingPlayMode`: the services outlive the last
  scene, and `ExitingPlayMode` fires while it is still alive, so every `OnDisable` / `OnDestroy`
  that reads a service would throw "No ServiceRegistry is armed" (40 errors per Stop, 2026-10-04).
  `PlayerLoopHook` is the one thing that goes on `ExitingPlayMode`: the loop outlives Stop and
  nothing should tick during teardown. Tests: `ServiceLocatorTests` and `TimerBootstrapperTests`
  in `Utility`, `GameLoopTests` here.
- **The Play-exit check** (`Assets/Submodules/Utility/Editor/PlayExitCheck.cs`) is the only thing
  that sees teardown errors: a PlayMode test cannot stop Play Mode. It enters Play in the scene
  you name, steps 20 frames, stops, and fails on any `Error`/exception/assert logged before Edit
  Mode is back. Run it with the project closed, after touching anything `OnDisable`, `OnDestroy`,
  a service's lifetime or a static cleared on leaving Play Mode:
  `Unity.exe -batchmode -nographics -projectPath <proj> -executeMethod Submodules.Utility.Editor.PlayExitCheck.Run -playExitScene Assets/Scenes/Example.unity -logFile out.log`.
  The **exit code is the verdict** (0 clean, 1 teardown errors listed under `[PlayExitCheck]`, 2
  never got back to Edit Mode, 3 no scene given), unlike `-runTests`. It is inert unless `Run`
  started it, so it is the one `-executeMethod` script that is committed on purpose.
- **`Application.quitting` fires on an Editor Stop with domain reload disabled** (verified 2026-10-05,
  Unity 6000.6.0f1, `m_EnterPlayModeOptions: 3`). `GameExit` (the quit save, #170) hangs off it: a Play-exit
  check run showed the hero written a second time at Stop, ~1 s after the Play-entry write, and 0
  teardown errors. The handler is released on `EnteredEditMode`, like the locator's clear. To re-verify,
  run the Play-exit check with `-savesFolder <scratch>` and look for `<id>.sav.bak` beside `<id>.sav`.
- **`-savesFolder <path>` points the boot's save store at a scratch folder.** The boot continues the
  last hero (or creates one) on every Play entry, so any headless run that enters Play Mode writes
  hero files. Pass it to the Play-exit check and to `-runTests -testPlatform PlayMode`; without it the
  files land under `Application.persistentDataPath/saves`, the player's real saves. EditMode tests
  never need it (`GameBoot.Build`/`Arm` default to an in-memory store).
- **A PlayMode test cannot assume the boot's services.** `CharacterStatPanelPlayModeTests` re-arms the
  locator with `GameBoot.Arm(config)` (in-memory saves, no hero loaded), so a later fixture sees that,
  not the Play-entry boot. Assert boot wiring that `Arm` does not touch (`GameExit.IsInstalled`), not state.
- **The boot's `[RuntimeInitializeOnLoadMethod]` hooks are only reachable from PlayMode.**
  `Assets/Scripts/Tests/PlayMode/Services/` proves they fire; EditMode `Run All` does not include
  it, run it with `-testPlatform PlayMode`.
- **A PlayMode test asmdef must not be `includePlatforms: ["Editor"]`.** The EditMode runner
  then picks the fixture up and runs it with Play Mode off (it fails, and the PlayMode run skips
  it as editor-only). Leave `includePlatforms` empty and guard `AssetDatabase` behind
  `#if UNITY_EDITOR`.

## Shared working directory, worktrees, and the `Utility` submodule

The primary working directory and the `Assets/Submodules/Utility` checkout are **one folder on
disk, shared across concurrent sessions and branches.** Open `git worktree add <path> -b <branch>`
for any new independent line of work (an issue, a prototype, a docs/spec pass); plain checkout only
continues the task already checked out. Mixing work into one checkout cross-contaminated two
branches once, and the untangle hit a submodule merge conflict. Before trusting a compile error
that doesn't match your diff:

1. `git branch --show-current` — the checked-out branch can change between turns (another
   session or the user switches it) without you running `git checkout`. Uncommitted
   changes survive a non-conflicting switch, so you can find yourself on the wrong branch
   with your edits still present.
2. `git submodule status Assets/Submodules/Utility` — a leading `+` means the submodule's
   checked-out commit ≠ what the current branch pins. Switching the parent branch does
   **not** re-checkout the submodule (no `submodule.recurse` here), so a branch can end
   up compiled against another branch's newer `Utility`. This produces real-looking
   `CS0104` ambiguous-reference errors (two classes, same short name, two namespaces)
   that are not bugs in your branch. Fix with `git submodule update <path>`, never by
   editing source to route around the ambiguity.

**A `Utility` change lands in two steps: commit and push the submodule, then pin it in the parent**
(bumping first is how #79 happened). `git submodule update` leaves the submodule on a detached HEAD, so
a commit made there belongs to no branch. `dev/bump-utility.sh "<parent commit message>"` does the rest:
it attaches HEAD to a branch named after the parent's (without moving it), pushes it, and commits only
the pin. Run it once the Utility commit is made, not before.

## Test fixtures for the Services tests

`Assets/Scripts/Tests/EditMode/Services/` has the fixtures a persistence or simulation test needs;
reach for them before writing a local copy.

- **`TestGameConfig.Create(created)`** builds a `GameConfig` from the authored assets with the test's
  own coin odds. Its fields are written through `SerializedObject` with `<Name>k__BackingField` keys
  (`DefaultHero`, `Catalog`, ...), which is also how a test swaps a template.
- **`TestGame.Create(config)`** is one whole game (items, session, simulation) as a boot builds it.
  **`game.SavesOver(config, store)`** adds its save service. Share *one* store between two games to
  save in one and load in the other; a fresh store per game sees nothing.
- **`TestLocations.Create` / `Author`** make Locations with stable ids and make them the config's
  authored list. A Location made but not authored has no saveable id.
- **`TestPackages.PickUp(container, cell)`** is a drag pick-up. `RemoveAtPosition` returns what is
  *left over*, not what was removed, so the hand is the package read before the removal.
- A restore or save test that expects an `Error` log must name it (`LogAssert.Expect`); EditMode fails
  on an unexpected one.

## Knowledge that lives outside git

Agent memory is per-machine; this file, `GLOSSARY.md`, `docs/adr/`, `dev/specs/` and GitHub Issues
are the shared channels. A fact that matters on both the laptop and the tower belongs in one of them.

- **`NewArtwork`** branch (off an old `main`, pushed, no PR) is the only copy of three
  imported Asset Store UI packs under `Assets/Art/` (`GUI_Parts`, `Modern GDR - Free
  icons pack`, `RVFX / UIShaderEffects-EdgeEffects`, ~53 MB committed raw). Pull
  selectively into `main`/features, never wholesale. RVFX ships both a uGUI/Canvas and a
  UI Toolkit implementation; this project is Canvas-based, so the uGUI half is the usable one.
- **This repo uses no Git LFS.** Don't set one up without asking — it was tried once for
  a 279 MB audio pack that the user then rejected, and reverted. The only committed
  binaries are 6 pre-existing `*.dll`s.
