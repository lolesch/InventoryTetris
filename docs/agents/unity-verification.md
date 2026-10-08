# Unity Verification

How to prove a change compiles, passes its tests and drives the scene, against this repo's Editor,
bridge and batch mode. Read before running tests, before a `Unity_RunCommand`, and before scene
authoring on a worktree. Other engineering gotchas live in `codebase-notes.md`.

---

## Running the tests headless

`dev/run-tests.sh [EditMode|PlayMode] [filter]` runs the suite with the Editor closed and prints the
compile errors, the totals and each failing test's message; its exit status is the verdict
(Unity's exit code is not). The filter is Unity's `-testFilter` (a test, fixture or namespace,
`;`-separated), so `dev/run-tests.sh EditMode HeroSaveServiceTests` is a ~25 s loop and the full
EditMode suite ~90 s. It restores `ProjectSettings`, which every run rewrites, and sends a PlayMode
run's saves to a scratch folder (`-savesFolder`, below).

**With the Editor open on the project the script runs in a shadow copy by itself**: it detects the
project lock, mirrors `Assets`, `Packages` and `ProjectSettings` into
`%TEMP%/<project>-shadow/<branch>` (`SHADOW_DIR` relocates it, `--shadow` as the first argument forces it)
and runs there. The shadow keeps its own `Library/`, so only its first run pays the full asset import
(several minutes); later runs are the mirror plus a compile. The checkout is never touched. Do not
delete `Temp/UnityLockfile`: two processes writing one `Library/` corrupts it. A hand-built `Unity.exe
-runTests` line is only for `-executeMethod`, never for the suite.

## Verifying a C# change compiles

**`dotnet build` lies here.** The generated `.csproj`s are stale — they omit newer
scripts and reference package versions no longer in `Library/PackageCache`, so `dotnet`
reports phantom `CS2001`s while silently not compiling the real change. A false green has
also happened (a Windows `csc.exe` handed `/tmp/...` paths, never ran, reported "0
errors").

**Drive the `unity-mcp` bridge against the running Editor instead:**

1. Recompile via `Unity_RunCommand`. **The code must be a full `IRunCommand` class**: a bare
   statement fails with `CS8805` (top-level statements), and the wrapper imports no namespaces, so
   every type needs its `using` (a missing `using UnityEngine;` reads `CS0103: 'Object' does not
   exist`). `Image` binds to a namespace in this project (`CS0118`): alias it,
   `using UImage = UnityEngine.UI.Image;`.
   ```csharp
   using UnityEditor;

   internal class CommandScript : IRunCommand
   {
       public string Title => "Recompile";
       public void Execute(ExecutionResult result)
       {
           AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
           global::UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
           result.Log("requested");
       }
   }
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
regex; same `IRunCommand` class wrapper as above), then wait with `dev/wait-editmode.sh` started with
`run_in_background` — it blocks until the file reads `DONE` and exits 0 only on `failed=0`. A bare
`sleep N` in the Bash tool is rejected by the harness. The human
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
command lines show which project each instance holds. Fall back to `dev/run-tests.sh --shadow`
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
