# Codebase Notes

Durable, hard-won facts about how *this* repo behaves that you can't get from the code, git history, or CLAUDE.md. Read before doing Unity compile/test verification, before touching assembly definitions, and when a build error doesn't match your diff.

This file is the shared channel for that knowledge across machines — an agent's private memory does not travel between devices, this does. Keep it current; prune what stops being true.

---

## Verifying a C# change compiles

**`dotnet build` lies here.** The generated `.csproj`s are stale — they omit newer
scripts and reference package versions no longer in `Library/PackageCache`, so `dotnet`
reports phantom `CS2001`s while silently not compiling the real change. A false green has
also happened (a Windows `csc.exe` handed `/tmp/...` paths, never ran, reported "0
errors").

**Drive the `unity-mcp` bridge against the running Editor instead:**

1. Recompile via `Unity_RunCommand`:
   ```csharp
   AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
   global::UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
   ```
   Fully-qualify `CompilationPipeline` — the tool wraps your snippet in
   `Unity.AI.Assistant.Agent.Dynamic.Extension.Editor`, where a bare name binds wrong.
   Prefer letting Unity recompile on its own (focus / file-change); each forced
   `RequestScriptCompilation()` costs a ~20–40 s domain reload during which the bridge
   drops and reconnects.
2. Read errors with **`Unity_GetConsoleLogs`**. `Unity_ReadConsole` has repeatedly
   returned 0 entries for a real compile error — do not trust it.
3. Ground truth when a `RunCommand` claims success against possibly-stale assemblies:
   tail `~/AppData/Local/Unity/Editor/Editor.log` past a line marker and grep
   `error CS[0-9]` / `Tundra build (failed|success)`.
4. **Run a negative control before believing a green**: inject a deliberate
   `DELIBERATE_SENTINEL_ERROR`, confirm it surfaces, revert.

**Editor closed?** Batch mode compiles the whole project *and* runs tests:
```
Unity.exe -runTests -batchmode -projectPath <proj> -testPlatform EditMode \
  -testResults <out.xml> -logFile <out.log>
```
~2–4 min. Trust the parsed `<test-run ... passed= failed=>` and `grep -c "error CS"`,
not the exit code.

**Running the EditMode suite through the bridge** (works with the Editor open): `Unity_RunCommand`
`ToolSmiths.InventorySystem.EditorScripts.EditModeTestRunner.Run();` (or `Run("Wallet")` to scope by
regex), then poll `Temp/editmode-results.txt` for `DONE`, not for the file's existence. The human
`Run All` in the Editor is still the official gate before closing an issue; this is the pre-check.
Source: `Assets/Editor/EditModeTestRunner.cs`.

**`Unity_RunCommand` reflection restriction:** any dynamic bridge script that touches
`System.Reflection` (even just the `using` plus a `BindingFlags` expression) throws an
uncatchable `UNEXPECTED_ERROR: Object reference not set`. `typeof(X)` and
`AppDomain.CurrentDomain.GetAssemblies()` are fine; `GetMethod`/`GetField`/`SetValue`
are not. Workaround: put a `public static` harness class in a real file under
`Assets/Scripts/` (it lands in `Assembly-CSharp`, sees internals) and have `RunCommand`
call one method on it — reflection inside a *compiled* file runs fine.

**Other:** Unity content-hashes source, so `touch` alone won't retrigger a compile.

**Scratch runners and `.bak` files are caught by the pre-commit hook.** Enable it once per clone:
`git config core.hooksPath dev/hooks`. It rejects a staged `TestRunnerApi` bridge script or
`-executeMethod` runner (a file under `Assets/` that calls `EditorApplication.Exit(`,
`TestRunnerApi` or `DELIBERATE_SENTINEL_ERROR`; tests and the `Utility` submodule are exempt,
and `allow-scratch-runner` in a file exempts it), a `.bak` or `.bak.meta` (Unity emits that
`.meta` for a `.bak` beside a script), a cut `.cs.meta`, and mojibake in added lines. Delete the
runner and its `.meta` yourself when the run is done; the hook is the backstop.

## Scene authoring and verification on a worktree

The `unity-mcp` bridge only serves the Editor's own project path, which reads as "a change
needing both scene authoring and a compile/test gate has to happen in the primary checkout".
It doesn't — **batch mode works on a worktree while the Editor holds the primary project
open**: different project path, different `Library/`, no lock conflict, and a second Editor
instance beside this project's Personal license is fine (verified 2026-09-21).

1. `git worktree add <path> -b <branch> <base>`, then
   `git submodule update --init Assets/Submodules/Utility`. A fresh worktree's submodule
   folder is **empty**, and nothing compiles without it.
2. Author the scene from a `public static void Run()` in a script under `Assets/Editor/`
   (that folder lands in `Assembly-CSharp-Editor`, which sees `Assembly-CSharp`), driven with
   `-executeMethod <Class>.Run`. `EditorSceneManager.OpenScene` the scene, edit components and
   `SerializedObject` fields, then `MarkSceneDirty` + `SaveScene`.
   To change a component's *class*, destroy it and `AddComponent` the replacement: the
   serialized values are lost, so read them from a `SerializedObject` first and re-apply them.
   Destroying a component also nulls every reference to it (the `PanelGroup` case, #85), so
   dangling `fileID` slots are less of a hazard than they look — but confirm, because a missing
   reference is silent.
3. Verify inside the same run: dump the wiring the change depends on before
   `EditorApplication.Exit(0)`. A multi-line `Debug.Log` report lands intact in `-logFile`.
4. `-runTests -testPlatform EditMode` for the suite. Neither step needs the scene open.

**The first run on a new worktree pays a full asset import** (several minutes, mostly
textures) and builds its own `Library/`; later runs are a compile plus the tests, ~90 s.
**`-executeMethod` aborts before running when the project has any `error CS`**, so the first
run doubles as a compile gate — grep the log for `error CS` and for `Aborting batchmode`.
Delete the `-executeMethod` script and its `.meta` when done (the pre-commit hook above
rejects it).

**The Editor beats you to it.** Opening a worktree in the Editor (a human does this to smoke
a change) takes the project lock and every later `-runTests` there aborts in under a second
with only `Exiting without the bug reporter` and no import in the log — which reads like a
broken Unity, not like a lock. `tasklist //FI "IMAGENAME eq Unity.exe"` plus the process
command lines show which project each instance holds. Fall back to the shadow copy above
rather than deleting `Temp/UnityLockfile`.

## Driving Play Mode to verify UI wiring by hand

`Unity_ManageEditor Play` enters Play Mode; then `Unity_RunCommand` a harness method (see
the scratch-harness pattern above) and read the results it logs.

- **Advance frames yourself.** With `Application.runInBackground=False` and the Editor
  unfocused, the player loop is frozen: `Time.time` stays 0.000 and tweens never finish,
  so a panel you just toggled sits mid-fade forever. `EditorApplication.Step()` in a loop
  (30–60 frames) is what makes fades and `CanvasGroup` state settle.
- **One `LogError` leaves the Editor paused**, which then hangs every later `Step()` — the
  bridge waits out its 120 s, comes back `COMPILATION_IN_PROGRESS`, and it reads as a dead
  Editor. Console **Error Pause** is ON here and the two pre-existing
  `AbstractProvider.OnValidate` errors fire on every Play, so this is the normal case, not
  an edge. Set `EditorApplication.isPaused = false` before *each* `Step()`. Diagnose with
  `Unity_ManageEditor GetState`: it reports `IsPaused` / `IsCompiling` / `IsPlaying`, where
  the other tools just hang.
- **`AssetDatabase.ImportAsset` while playing exits Play Mode**, and the static provider
  `Instance`s are left stale — a harness call right after NREs on `Sim.Run`. After any
  script edit: re-import, check `GetState`, re-enter Play, then verify.
- **A "click" is `SetToggle(!IsOn)`**, matching `AbstractToggle.OnClick` — not
  `SetToggle(true)`. Calling `SetToggle(true)` on a toggle that is already on is a silent
  no-op, which reads as "the click did nothing" or, worse, looks like a pass. Off-click
  paths only exist where the group allows them (`IsClearable` / `IsRestorable`).
- Assert the wiring as a **triple**: the panel's `CanvasGroup.alpha`/`blocksRaycasts`, the
  provider's announced context, and `RadioGroup.ActiveMember`. "Toggle off" and "panel
  hidden" are different facts and disagreeing is exactly the bug class this catches.
- **Setting `EditorApplication.isPlaying = false` from inside `Unity_RunCommand` can leave the
  bridge stuck** — the call itself times out client-side, and every subsequent `RunCommand`
  (even a trivial one-line log) then times out too, for ten-plus minutes, while
  `Unity_GetConsoleLogs` keeps working the whole time. Waiting it out, recompiling, and
  focusing/clicking the Editor window (tried via `SetForegroundWindow` and a synthetic click
  on the title bar) did **not** unstick it. A human clicking **Stop** in the Editor's own
  Play toolbar did, immediately. Confirmed recurring across sessions 2026-09-20. Prefer asking
  the user to stop Play Mode by hand over requesting `isPlaying = false` from a script when a
  Play-Mode verification pass is done; if a script-driven stop is already in flight and the
  bridge goes unresponsive, stop retrying and ask the user to press Stop rather than waiting
  it out.

### Play Mode checks in batch mode, no Editor needed (verified 2026-10-04, #127)

The worktree route above also runs *Play Mode*. Write a `public static void Run()` under `Assets/Editor/`
that calls `PlayModeDriver.Start(scenePath, Routine)` and run it with `-executeMethod`; the exit code is the
verdict (0 pass, 1 failed check or logged error, 2 timeout). Whole run ~15 s once the worktree is imported.
`Assets/Editor/PlayModeDriver.cs` handles what makes a bare script lie: it steps the player loop every tick,
runs yielded routines through a stack, and ignores Unity Search's startup exception. Wait with
`PlayModeDriver.Wait` (on `Time.time`); `PlayModeDriver.SelfTest` is the template. Verified in batch
2026-10-05.

Assert the triple from the section above (`CanvasGroup` alpha, announced context, group `ActiveMember`).
A private field such as a display's bound `Container` is readable by reflection from the compiled Editor
script.

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

## `com.unity.ai.assistant` — the bridge package

Backs `unity-mcp`; pinned at `2.20.0-pre.1`. The manifest diff does not say why these versions are
off limits:

- `2.7.0-pre.3` to `2.15.0-pre.2` cap MCP connections by Unity license tier; on this project's
  **Personal** license they throttle the bridge. `2.16.0-pre.1` lifts the cap.
- `2.13.0-pre.2` has an external livelock report on Unity 6000.5.1f1
  ([CoplayDev/unity-mcp#1219](https://github.com/CoplayDev/unity-mcp/issues/1219)); not reproduced here.
- `2.6.0-pre.1` trips Unity 6.6's `UAC0005` analyzer as a hard error.

To upgrade, jump straight to the current release rather than stepping through minors. Verified
2026-09-18 at `2.19.0-pre.2` on Unity 6000.6.0f1: 0 `error CS`, EditMode suite green headless and
through the bridge, every gotcha under "Verifying a C# change compiles" unchanged. The reflection
restriction there is now a named, catchable error (`Script uses one or more unauthorized
namespaces: Namespace System.Reflection ...`); the workaround is the same.

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

## The "Scene(s) Have Been Modified" modal

Unity's Save / Don't Save / Cancel scene dialog is a **blocking native OS modal** — once
it is up, no editor code runs until a human clicks, so it cannot be auto-dismissed, only
*prevented*. `Assets/Submodules/Utility/Editor/SceneSavePromptGuard.cs` (it moved into the
submodule — it is not under `Assets/Scripts/`) (`ToolSmiths.InventorySystem.EditorScripts`,
`[InitializeOnLoad]` + `EditorApplication.update`
poll) clears the dirty flag for scene dirt that *originated while Unity was in the
background*, after a ~1 s debounce, via reflected `EditorSceneManager.ClearSceneDirtiness`.
Dirt made while Unity is **focused** is left alone (no data loss on hand edits). Menu:
**Tools ▸ MCP ▸ Suppress Scene Save Prompt** (default ON). Entering Play Mode never
prompts.

**Consequence for you:** when you drive the Editor via `unity-mcp` and *intend* a scene
edit to persist (wiring a button, adding an object to `Example.unity`), call
`EditorSceneManager.SaveOpenScenes()` **in the same `RunCommand`** — otherwise the guard
treats it as background dirt and discards it ~1 s later. Explicit saves always win.

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
