# Input (ADR-0019, #69)

Active Input Handling is the Input System package, so **any `Input.GetKey` / `Input.mousePosition` read
throws in Play**; the pre-commit hook rejects an added one. A view reads `GameInput.IsHeld` /
`GameInput.PointerPosition` and subscribes to `GameInput.Performed` in `OnEnable` (release it in
`OnDisable`). That event outlives a boot's service, so subscribe to it, never to `GameInput.Armed`'s.

- **Package version is tied to the Editor.** `com.unity.inputsystem` 1.18.0 does not compile on Unity
  6000.6 (`CS0619` on `EntityId`'s int conversion in `PlayerInputEditor`); 1.20.1 does. A sibling repo on
  an older Editor can stay on 1.18.0.
- **Do not hand-queue device events in a batch-mode driver.** `InputSystem.QueueStateEvent` on a virtual
  device is silently dropped there (no focus, and the Edit-to-Play transition window), whatever the
  background settings. Drive input with the package's `InputTestFixture` (`Press`, `Set`): EditMode in
  `Utility/Tests/EditMode/Bindings`, PlayMode in `HotkeyPlayModeTests`. The fixture isolates the Input
  System, which drops the boot's actions, so a PlayMode test boots again over it
  (`ServiceLocator.Reset(); GameBoot.Arm(...)`).
- **A bare `InputSystemUIInputModule` is the right scene state.** Its `OnEnable` assigns the package's
  default UI actions when none are serialized. Calling `AssignDefaultActions()` from an editor script
  instead serializes a copy of the whole asset into the scene (+800 lines per scene). Swap with
  `DestroyImmediate(StandaloneInputModule)` + `AddComponent<InputSystemUIInputModule>()`.
- **The shared asset is never enabled.** `InputService` works on a clone (`Object.Instantiate`), because
  with domain reload off the ScriptableObject outlives Play and would carry rebindings and enabled state
  into the next session. The registry owns the service, so `ServiceLocator.Reset()` disposes the clone;
  a test that arms calls `Reset` in its teardown and leaks nothing.
- **A hotkey pressed twice within 0.15 s of input time is one press** (`GameBoot.HotkeyRepeatSeconds`). A
  test that presses the same action twice advances `currentTime` between them.
- **`Utility/UI/Demo.unity` still has a `StandaloneInputModule`**, which errors every frame in this
  project. It is a submodule demo shared with repos on the legacy handler; swap it only there.
- **Serialized `GameAction` values are scene data** (a toggle's `hotkey`). Append a member, never
  renumber one.
