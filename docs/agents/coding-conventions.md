# Coding Conventions

## Lazy GetComponent caching

```csharp
field ? field : field = GetComponent<T>();
```

Use when the lookup always succeeds (same GameObject + `[RequireComponent]`).

An optional lookup (e.g. component on a *parent*) can stay null forever, and null can't
tell "not looked up yet" from "looked up, none found" — every access re-runs
`GetComponent`. Resolve explicitly instead:
- `Awake()` — once, covers runtime-instantiated objects.
- `OnValidate()` — re-resolve when the relationship can change at edit time (reparent).

Reassign the cached ref *after* any call using the old value — else identity guards
elsewhere (`x.Owner != this`) see the new value early and no-op.

## Auto-properties over backing fields

```csharp
[field: SerializeField, ReadOnly] public T X { get; private set; }
```

replaces `[SerializeField, ReadOnly] protected T x = null; public T X => x;`.

`protected set` only if a subclass writes it directly.

## Ownership guards on public mutators

A mutator that another assembly can call keeps its `toggle.Owner != this` guard even when every
current call site satisfies it. Inside an assembly a guard needs a reachable violation (see
*Minimal code*).

## Minimal code (YAGNI)

Every branch, guard, helper, parameter, public member, attribute and test names the call path
that reaches it. No path, no line.

- **Reachable**: a guard handles a state some caller can produce. A state only a reflection-set
  test field produces gets no branch and no test.
- **One primitive**: two methods that differ by a guard share one primitive that takes the
  difference as an argument.
- **Internal until needed**: a member is public when a caller in another assembly needs it today.
- **Failing test after a simplification**: first ask whether the test's state is reachable. If not,
  delete the test and say so; if so, report the gap to the user rather than restoring the code.
- **A deletion by the user stands**: Read the file again before editing it; never re-add what the
  user removed.
- **Doc comment**: one or two lines of why. It names no sibling members and doesn't narrate what
  was removed.

## Test level

A rule is tested in EditMode at its service seam (`ISimulationService`, `ISession`, `IInventoryService`, a
flow such as `MainMenuFlow`). A PlayMode test proves only what Play Mode alone reaches:

- the boot's `[RuntimeInitializeOnLoadMethod]` hooks and a scene load;
- **wiring**: one smoke per panel or input that the scene binds it to its service;
- a regression that only a scene reproduces (teardown, a rebind after a hero load).

Feel (timing, easing, hover scale, the order of animation beats) is checked by playtest. A playtest note
about feel changes the code and leaves the tests alone. A PlayMode test drives the simulation with
`Tick` and waits on a condition (a scene loaded, a panel up), not on a duration.

## Public API needs an interface

Every class or MonoBehaviour that other systems call into gets an `IThing`, and callers
depend on that instead of the concrete type. Keeps call sites mockable and keeps
serialized fields and Unity lifecycle methods out of the contract.

## Screens, lists and displays

Judgement calls the hook cannot see; `/code-review` checks each on any change that adds or edits a UI
component.

- **A screen the player can dismiss registers with `ICancelChain`** at a `CancelRank`: the Pause Menu,
  the hero popups and the Changelog do. An unregistered screen ignores Esc.
- **A list that can outgrow its rect sits in a masked `ScrollRect`**, as the Changelog tab bodies do.
- **A display refreshes on every event that changes what it shows.** A resource bar follows the total as
  well as the current value: on `CurrentHasChanged` alone, equipping a +max-health item while paused left
  the bar stale.

## Enforced by the pre-commit hook

`dev/hooks/pre-commit` (enable: `git config core.hooksPath dev/hooks`) rejects what a linter would:

- A class that nothing inherits and that is not `sealed`, `abstract`, `static` or `partial`.
- A `using UnityEditor;` outside an `Editor`/`Tests` folder that is not under `#if UNITY_EDITOR`
  (a player build has no `UnityEditor.dll`, so it fails with `CS0246` even if nothing runs).
- An added `[FormerlySerializedAs]` (see the serialized-rename rule below).
- An added class named `*Panel` that does not derive `SimplePanel` (`GLOSSARY.md`, **Panel**): rename it
  to what it is.
- An added `WaitForSeconds` or `Time.timeScale =` in a test: it waits out or speeds up an animation, which
  is feel (see *Test level*).

Files that compile editor-only by construction (asmdef `includePlatforms: ["Editor"]`, an
`Editor`/`EditMode` folder; see `codebase-notes/play-mode-and-assemblies.md`) need no guard.

## Reverse `for` loops

```csharp
for (var i = list.Count; i-- > 0;)
```

`i` is already the last valid index inside the body.

## Renaming a serialized field: fix the scenes, no `FormerlySerializedAs`

When a rename changes a serialized name (a `[field: SerializeField]` property's
`<Name>k__BackingField` included), rewrite the old name in every `.unity`, `.prefab` and asset that
uses it in the same change, and add no `[FormerlySerializedAs]`. The attribute stays as dead
migration code long after the last file is updated. Grep `Assets/` for the old name; the scenes are
text YAML, so `sed` handles the rewrite.
