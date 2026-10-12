---
status: accepted
---

# Input goes through the Input System package; the mechanism lives in Utility, the catalog does not

Issue #69 asked for a rebindable input service and left "package or hand-rolled" open. Input was
read where it was needed: `Input.GetKey(KeyCode.LeftShift)` in three views, `Input.mousePosition` in
two providers, a `KeyCode` field on `SidePanelToggle`, and a polled Space in `PauseHotkey`. None of
it could be rebound, and a test could not press a key.

## Decision

1. **The `com.unity.inputsystem` package, not a hand-rolled registry.** It already supplies the
   action asset, interactive rebinding, override JSON and virtual devices for tests. Active Input
   Handling is **Input System Package (New)**: the legacy `Input` class throws in Play, so a stray
   read cannot hide, and the pre-commit hook rejects a new one. The scenes' `StandaloneInputModule`
   is an `InputSystemUIInputModule`.
2. **The mechanism is `Utility.Bindings`; the catalog is the game's.** `IInputService<TAction>` /
   `InputService<TAction>` take the catalog as an enum, clone an `InputActionAsset`, raise
   `Performed` for a button press, answer `IsHeld` and `PointerPosition`, rebind interactively and
   persist the overrides through an `IBindingOverrideStore`. It learns no game concept. `GameAction`
   and `Resources/GameInput.inputactions` (the default keys) stay in the game, as the save format
   stays out of Utility (ADR-0017). The asmdef compiles only where the package is installed
   (`versionDefines`), so a repo without it is unaffected.
3. **Members are named for intent, never the key** (`QuickMove`, `Peek`, not `Shift`, `Alt`). The
   player rebinds the key; the code asks for the intent. `GameInput` is the one static a view reads
   (held keys, pointer) and answers "nothing held" when no service is armed.
4. **Presses are events, modifiers are state.** A toggle or a provider subscribes to `Performed` in
   `OnEnable` and lets go in `OnDisable`; a view that needs "is Shift down while I click" asks
   `IsHeld`. Nothing polls a press.
5. **The service works on a clone of the asset.** With domain reload disabled a ScriptableObject
   outlives the Play session, and rebindings and the enabled state would leak into the next one.
   `GameBoot.Arm` builds and enables it (not `Build`, which touches no global) and disposes the
   previous boot's.
6. **Rebindings live in `PlayerPrefs`**, with the other `UserSettings`: they follow the player, not
   a hero save, and `SaveLocation.WipeAll` must not clear them.

## Not decided here

The settings screen that calls `Rebind` is not built; the service and its persistence are. Escape
cancels a rebind, so it cannot be bound to an action through the screen.
