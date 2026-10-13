# Test fixtures for the Services tests

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
- **`FakeInput`** is an `IInputService<GameAction>` a test presses by hand (`Press`, `Release`,
  `PointerPosition`); `PauseHotkey` and any class taking the service by constructor use it.
