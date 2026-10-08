# Unity Verification

How to prove a change compiles, passes its tests and drives the scene in batch mode, on the primary
checkout or any worktree. Read before running tests, a compile check, or scene authoring on a
worktree. Driving the *running* Editor through the `unity-mcp` bridge (`Unity_RunCommand`, Play Mode
by hand) is in `unity-bridge.md`. Other engineering gotchas live in `codebase-notes.md`.

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


`dev/run-tests.sh` (above) compiles the whole project and runs the tests; `error CS` in its output is the
compile verdict. Against a running Editor use the bridge route in `unity-bridge.md`.

**Editor closed?** Batch mode compiles the whole project *and* runs tests:
```
Unity.exe -runTests -batchmode -projectPath <proj> -testPlatform EditMode \
  -testResults <out.xml> -logFile <out.log>
```
~2–4 min. Trust the parsed `<test-run ... passed= failed=>` and `grep -c "error CS"`,
not the exit code.

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

## Play Mode checks in batch mode, no Editor needed (verified 2026-10-04, #127)

The worktree route above also runs *Play Mode*. Write a `public static void Run()` under `Assets/Editor/`
that calls `PlayModeDriver.Start(scenePath, Routine)` and run it with `-executeMethod`; the exit code is the
verdict (0 pass, 1 failed check or logged error, 2 timeout). Whole run ~15 s once the worktree is imported.
`Assets/Editor/PlayModeDriver.cs` handles what makes a bare script lie: it steps the player loop every tick,
runs yielded routines through a stack, and ignores Unity Search's startup exception. Wait with
`PlayModeDriver.Wait` (on `Time.time`); `PlayModeDriver.SelfTest` is the template. Verified in batch
2026-10-05.

Assert the triple (`CanvasGroup` alpha/`blocksRaycasts`, the provider's announced context, `RadioGroup.ActiveMember`):
"toggle off" and "panel hidden" are different facts, and disagreeing is the bug class this catches.
A private field such as a display's bound `Container` is readable by reflection from the compiled Editor
script.
