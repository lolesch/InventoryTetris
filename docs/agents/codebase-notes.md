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

## Enter Play Mode Settings — domain/scene reload disabled

`ProjectSettings/EditorSettings.asset` now has `m_EnterPlayModeOptionsEnabled: 1` /
`m_EnterPlayModeOptions: 3` (both `DisableDomainReload` and `DisableSceneReload`) —
applied 2026-09-18, verified live via the bridge
(`EditorSettings.enterPlayModeOptionsEnabled` / `.enterPlayModeOptions`) on the
`test/unity-6.6` worktree. Default was off; this is not 6.6-specific and is safe to
carry onto any branch.

**Read the "stale provider `Instance`" bullet above before relying on this.**
`AbstractProvider<T>.Instance` (`Assets/Submodules/Utility/Provider/AbstractProvider.cs:24`)
self-heals via Unity's overridden `== null` (true for a destroyed-but-not-literally-null
object), not via domain reload — Play Mode exit always destroys play-only objects
including `DontDestroyOnLoad` ones, reload or not, so a fresh `Instance` lookup on
re-entry should still work. But the *existing* stale-static gotcha above was observed on
an *irregular* exit path (`AssetDatabase.ImportAsset` while playing); disabling domain
reload makes every *ordinary* exit behave a little more like that path (no full managed-state
wipe in between). Re-validate with a few by-hand Play → Stop → Play cycles before trusting
provider state across repeated sessions — don't assume this note alone proves it's fine.
`SimulationProvider` and `TimerBootstrapper` both re-arm via
`[RuntimeInitializeOnLoadMethod]`, which fires on every Play Mode entry independent of
domain reload, so those two are already covered.

**The "re-validate" caution above found a real bug, 2026-09-19.** `AbstractProvider<T>`'s
static `_isQuitting` flag (`AbstractProvider.cs:11`) is set `true` by `OnApplicationQuit`
and never reset. In a build that's harmless — the process exits right after quitting, so
there's no next session to leak into — but with domain reload disabled the flag survives
Stop, and the *next* Play entry inherits `_isQuitting == true`, which makes
`AbstractProvider<T>.Instance` return `null` unconditionally for every provider
(`SimulationProvider`, `InventoryProvider`, `DragProvider`, `PreviewProvider`,
`ItemProvider`, `CharacterProvider`, `SceneProvider`) for the rest of the Editor session.
Symptom: `MinimapController.OnEnable` hit its `provider == null` guard and never called
`SyncToPhase`, leaving both `inTownPanel`/`inFieldPanel` faded out on the second Play
entry onward. Since this is purely an Editor artifact of disabled domain reload, the fix
lives in `Assets/Submodules/Utility/Editor/ProviderQuittingResetGuard.cs`, not in
`AbstractProvider<T>` itself: it resets every closed `AbstractProvider<T>`'s
`_isQuitting` via reflection on `EditorApplication.playModeStateChanged`'s
`ExitingEditMode` (the moment Play is pressed), mirroring what a fresh process would do.
Verified live via the bridge: `SimulationProvider.Instance` was `null` on a second Play
entry before the fix, resolves correctly after.

## `com.unity.ai.assistant` version — pin history and upgrade path

This package backs the `unity-mcp` bridge (`Unity_RunCommand`, `Unity_GetConsoleLogs`,
etc.) that the rest of this file assumes. Its version has moved twice for reasons that
aren't visible from the manifest diff alone:

- **Pinned at `2.6.0-pre.1`** (`115d4f4`) when the bridge was first added — the last
  version before Unity license-tier connection gating existed.
- **`2.7.0-pre.3` → `2.15.0-pre.2` cap MCP/AI-Gateway connections by Unity license
  tier** (Personal / Pro / Enterprise). On a **Personal** license (this project's) this
  range throttles the bridge. Lifted again in `2.16.0-pre.1` ("no longer capped or
  gated by entitlement limits").
- **`2.6.0-pre.1`'s own source trips Unity 6.6's `UAC0005` analyzer as a hard error** —
  why the `test/unity-6.6` spike branch (`62c2f78`) removed the package entirely rather
  than upgrade it.
- **`2.13.0-pre.2` has an external, gdb-traced livelock report** on Unity 6000.5.1f1
  (`AssetDatabase::InitialRefresh` spins forever) —
  [CoplayDev/unity-mcp#1219](https://github.com/CoplayDev/unity-mcp/issues/1219). Not
  reproduced by us, but avoid landing exactly on that version.

**Verified 2026-09-18**, on the `test/unity-6.6` worktree with Unity 6000.6.0f1 and the
Editor live-paired to the bridge: bumping straight to **`2.19.0-pre.2`** (current
release — skip past the capped range and the `2.13.0-pre.2` report rather than stepping
through it one minor version at a time) resolves clean, `0 error CS`, no `UAC0005`, and
the EditMode suite passes **698/698** both headless (`-runTests -batchmode`) and live
through the bridge itself. Every gotcha in this file's "Verifying a C# change compiles"
section still holds at `2.19.0-pre.2`:

- The reflection restriction is still enforced, but the error changed for the better —
  it used to be an uncatchable `UNEXPECTED_ERROR: Object reference not set`
  (`NullReferenceException`); it's now a named, catchable error: `"Script uses one or
  more unauthorized namespaces: Namespace System.Reflection is imported at line 1."` The
  workaround (put reflection in a compiled file under `Assets/Scripts/`) is unchanged.
- The two-top-level-class `ICallbacks`/`IRunCommand` pattern for running the EditMode
  suite through the bridge still compiles and runs as documented.
- The dynamic script wrapping namespace (`Unity.AI.Assistant.Agent.Dynamic.Extension.Editor`)
  is unchanged.

**Not yet exercised:** a manual Play-mode smoke pass on `2.19.0-pre.2`/6.6 — only
EditMode tests and ad hoc `RunCommand` scripts have run so far. The `test/unity-6.6`
worktree itself is a stale spike (its `main` merge-base predates this file, `e9fbf70`)
— treat its findings as validated guidance to replay on a fresh branch cut from current
`main`, not as a branch to build the real upgrade on top of.

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
importer block and never finishes it:

```bash
find Assets -name '*.cs.meta' -not -path '*/Library/*' | while read -r f; do
  grep -q MonoImporter "$f" && [ -n "$(tail -c1 "$f")" ] && echo "CUT $f"
done
```

**`grep -L assetBundleVariant` finds the opposite set.** The cut lands *at* that line, so a
**cut** meta still contains the string and the grep skips it; what it returns is every
**short** meta, all of them healthy. A scan built on it hands you a long, confident,
entirely wrong worklist — that is exactly how #80 got filed.

Measured 2026-09-19 (#80): **91 of 282** metas were **short** and **zero** were **cut**.
Copying `Assets Packages ProjectSettings` to a scratch dir with **no `Library/`** and
running EditMode there — the one check a warm Library cannot fake — gave 762/762 passing,
no `CS0103`/`CS0246`, no `Parser Failure`. The same fresh-import run after normalising all
91 gave the identical 762/762, and the `ForceReserializeAssets` pass that does it silently
drops each meta's `timeCreated:` line. Both Unity (auto-creating a meta for a script a tool
adds mid-compile) and hand-authoring produce the **short** shape; it costs nothing and
normalising it buys nothing.

To repair a genuinely **cut** meta: rewrite it canonically (`fileFormatVersion: 2` …
`assetBundleVariant: ` + trailing newline), **preserving the committed GUID** — grep
`Assets/Scenes/*.unity` for that GUID first; scene `m_Script` refs break if it changes.
Then `AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)` +
`CompilationPipeline.RequestScriptCompilation()` through the bridge.
## Source is CRLF + UTF-8 — stream editors corrupt it silently

Source under `Assets/Scripts/` is **CRLF-terminated UTF-8**, and the docstrings are dense
with em dashes (—) and other non-ASCII. There is no `.gitattributes`, so nothing normalises
this on commit. Two stream editors damage it without failing:

- **`sed -i` rewrites the file with LF endings even when it changes nothing**, so a glob
  like `sed -i 's/x/y/' dir/*.cs` marks every file in the directory dirty with a
  whole-file ending flip.
- **`perl -0pi -e` mojibakes existing UTF-8** (— becomes â) as soon as the replacement
  string itself contains a wide character — it switches to character semantics on output
  only.

Neither failure shows up in a test run: the code still compiles and the suite still passes,
so it reaches review as unrelated churn or as corrupted prose.

**Use an editor tool that preserves encoding and endings** (Claude Code's `Edit`) for
anything touching these files, even mechanical multi-file renames. If a stream editor is
genuinely the right tool, restrict the glob to files that will actually match, then check
`git diff --name-only` against `git status --short` and `git checkout --` anything that
shows modified with no content diff. This file and the rest of `docs/` are CRLF too.

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
