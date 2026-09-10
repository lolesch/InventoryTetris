# Codebase Notes

Durable, hard-won facts about how *this* repo behaves that you can't get from the code,
git history, or CLAUDE.md. Read before doing Unity compile/test verification, before
touching assembly definitions, and when a build error doesn't match your diff.

This file is the shared channel for that knowledge across machines — an agent's private
memory does not travel between devices, this does. Keep it current; prune what stops
being true.

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

**Running the EditMode suite through the bridge** (works with the Editor open): put two
**top-level** classes in one `Unity_RunCommand` script (the bridge's auto-fixer
duplicates nested/private ones) — an `ICallbacks` implementation that writes
`passed=N failed=M` to `Temp/results.txt` on `RunFinished`, and an
`IRunCommand`/`ICallbacks` that creates a `TestRunnerApi`, registers callbacks, and
`Execute`s an `ExecutionSettings(new Filter { testMode = TestMode.EditMode })`. Poll for
a *completion marker inside the file*, not file existence (it's created immediately then
updated). A `groupNames` regex on the `Filter` scopes to one namespace. The human
`Run All` in the Editor is still the official gate before closing an issue; the bridge
run is a real pre-check, not a replacement.

**`Unity_RunCommand` reflection restriction:** any dynamic bridge script that touches
`System.Reflection` (even just the `using` plus a `BindingFlags` expression) throws an
uncatchable `UNEXPECTED_ERROR: Object reference not set`. `typeof(X)` and
`AppDomain.CurrentDomain.GetAssemblies()` are fine; `GetMethod`/`GetField`/`SetValue`
are not. Workaround: put a `public static` harness class in a real file under
`Assets/Scripts/` (it lands in `Assembly-CSharp`, sees internals) and have `RunCommand`
call one method on it — reflection inside a *compiled* file runs fine.

**Other:** Unity content-hashes source, so `touch` alone won't retrigger a compile. A
`.bak` file next to a script makes Unity emit a stray `.bak.meta` — delete before
committing. Delete any `TestRunnerApi` bridge / scratch runner script before committing
(it has happened twice — a "Do NOT commit" header is not enough).

## `CS0103` / `CS0246` that is really a broken `.meta`

Hand-authored `.cs.meta` files in agent commits have shipped **truncated** (cut at
`  assetBundleVariant:` with no trailing newline) or **missing entirely**. Unity's YAML
parser rejects the truncated ones (`Parser Failure at line 11: Expect ':' ...`) and logs
"does not have a valid GUID and its corresponding Asset file will be ignored" — so the
`.cs` never compiles and every consumer fails with `CS0103: The name 'X' does not
exist`. It **looks like a missing-assembly / asmdef-reference problem and is not.**

A **warm Unity Library masks it** — a session that already force-imported good metas
into its AssetDatabase runs the whole suite green with the broken metas still on disk. A
fresh checkout / Library wipe breaks.

Detect: `AssetDatabase.AssetPathToGUID(path)` returning `""` confirms Unity ignored the
asset. Or `od -c file.meta` — a valid script meta is ~470 bytes / 11 lines ending in a
newline; a broken one is ~239 bytes ending mid-line.

Fix: rewrite the meta canonically (`fileFormatVersion: 2` … `assetBundleVariant: ` +
trailing newline), **preserving the committed GUID** — grep `Assets/Scenes/*.unity` for
that GUID first; scene `m_Script` refs break if it changes. Then
`AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)` +
`CompilationPipeline.RequestScriptCompilation()` through the bridge.

## The "Scene(s) Have Been Modified" modal

Unity's Save / Don't Save / Cancel scene dialog is a **blocking native OS modal** — once
it is up, no editor code runs until a human clicks, so it cannot be auto-dismissed, only
*prevented*. `Assets/Scripts/Editor/SceneSavePromptGuard.cs`
(`ToolSmiths.InventorySystem.EditorScripts`, `[InitializeOnLoad]` + `EditorApplication.update`
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
Re-derive the assembly graph folder-by-folder rather than trusting namespace names when
a reference won't resolve.

- **`Assembly-CSharp` has zero test coverage in this repo.** Any code that needs to be
  unit-tested must live in its own asmdef, not in `Assembly-CSharp`. This is why
  `LocationConfig` got `InventorySystem.Locations.asmdef` instead of the simpler home.
- **`InventorySystem.Simulation` and `InventorySystem.Locations` are Unity-free by
  convention, not by flag.** `noEngineReferences` is `false`, but files still get **no**
  implicit `using`. A file under `Assets/Scripts/InventorySystem/Simulation/` that needs
  `Mathf` must `using UnityEngine;`; one that needs `System.Math` must `using System;`.
  Check every new file here.
- **`Assets/Scripts/InventorySystem/Data/Distributions/*.cs` is its own
  `InventorySystem.Distributions.asmdef`** (custom asmdefs can't reference
  `Assembly-CSharp`, and `LootTable`'s only implementer was `internal` there). It
  resolves from `ItemProvider` (still `Assembly-CSharp`) only because it is
  `autoReferenced: true`. There is a matching `InventorySystem.Distributions.Editor.asmdef`
  for the two custom editors in that folder. Watch this whenever you touch
  `Data/Distributions/` or `ItemProvider.cs`.

## Shared working directory + the `Utility` submodule

The primary working directory and the `Assets/Submodules/Utility` submodule checkout are
**one folder on disk, shared across concurrent sessions and branches.** Before trusting a
compile error that doesn't match your diff:

1. `git branch --show-current` — the checked-out branch can change between turns (another
   session or the user switches it) without you ever running `git checkout`. Uncommitted
   changes survive a non-conflicting switch, so you can find yourself on the wrong branch
   with your edits still present.
2. `git submodule status Assets/Submodules/Utility` — a leading `+` means the submodule's
   checked-out commit ≠ what the current branch pins. Switching the parent branch does
   **not** re-checkout the submodule (no `submodule.recurse` here), so a branch can end
   up compiled against another branch's newer `Utility`. This produces real-looking
   `CS0104` ambiguous-reference errors (two classes, same short name, two namespaces)
   that are not bugs in your branch. Fix with `git submodule update <path>`, never by
   editing source to route around the ambiguity.

## Worktrees, not in-place checkout

For any new independent line of work (a new issue, a prototype, a docs/spec pass), open
`git worktree add <path> -b <branch>` rather than `git checkout` in the primary working
directory. Reserve plain checkout for continuing the task already checked out. Mixing new
work into the primary checkout's branch has silently cross-contaminated two branches that
shared a base commit, and the untangle hit a `Utility` submodule merge conflict.

## Working the issue tracker

Before `/implement #N`, walk that issue's own **Blocked by** chain (transitively, via
`gh issue view <N> --json body` or the epic's tracking table). `ready-for-agent` on an
issue means its spec is written, **not** that its dependencies are closed. If `#N` is not
the actual frontier, surface the gap and let the user decide — don't silently build the
blockers inside it (that blows past the "one ticket per session" rule) or silently
substitute a different issue.

## Multi-device / knowledge that lives outside git

- An agent's private memory is **per-machine** and does not sync. The shared, on-disk
  channels are: this file, `CONTEXT.md`, `docs/adr/`, `dev/specs/`, and GitHub Issues.
  If a fact matters on both the laptop and the tower, it belongs in one of those, not in
  memory.
- **`NewArtwork`** branch (off an old `main`, pushed, no PR) is the only copy of three
  imported Asset Store UI packs under `Assets/Art/` (`GUI_Parts`, `Modern GDR - Free
  icons pack`, `RVFX / UIShaderEffects-EdgeEffects`, ~53 MB committed raw). Pull
  selectively into `main`/features, never wholesale. RVFX ships both a uGUI/Canvas and a
  UI Toolkit implementation — this project is Canvas-based, so the uGUI half is the
  usable one.
- **This repo uses no Git LFS.** Don't set one up without asking — it was tried once for
  a 279 MB audio pack that the user then rejected, and reverted. The only committed
  binaries are 6 pre-existing `*.dll`s.
- `Assets/Scenes/Example.unity` is the only scene (`HUD.unity` was deleted as unused).
