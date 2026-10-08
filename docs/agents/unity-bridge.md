# Unity Bridge

Driving the *running* Editor through the `unity-mcp` bridge: compile, run tests, drive Play Mode.
Read before a `Unity_RunCommand` or any other `Unity_*` tool. Headless and worktree batch runs, which
need no open Editor, are in `unity-verification.md`. Other engineering gotchas live in
`codebase-notes.md`.

---

## Verifying a C# change compiles through the bridge

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
