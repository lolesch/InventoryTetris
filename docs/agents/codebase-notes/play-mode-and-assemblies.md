# Play Mode reload, statics and assembly definitions

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
- **A PlayMode test cannot assume the boot's services.** `CharacterStatSheetPlayModeTests` re-arms the
  locator with `GameBoot.Arm(config)` (in-memory saves, no hero loaded), so a later fixture sees that,
  not the Play-entry boot. Assert boot wiring that `Arm` does not touch (`GameExit.IsInstalled`), not state.
- **The boot's `[RuntimeInitializeOnLoadMethod]` hooks are only reachable from PlayMode.**
  `Assets/Scripts/Tests/PlayMode/Services/` proves they fire; EditMode `Run All` does not include
  it, run it with `-testPlatform PlayMode`.
- **A PlayMode test asmdef must not be `includePlatforms: ["Editor"]`.** The EditMode runner
  then picks the fixture up and runs it with Play Mode off (it fails, and the PlayMode run skips
  it as editor-only). Leave `includePlatforms` empty and guard `AssetDatabase` behind
  `#if UNITY_EDITOR`.
