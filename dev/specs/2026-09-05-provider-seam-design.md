---
status: candidate 1 shipped independently (see Status update, 2026-09-21); candidate 2
  still open and is what /to-tickets should slice
---

# The provider seam: stop the Play-mode shutdown resurrection, then hand `LocalPlayer` its containers directly

## Status update — 2026-09-21

Candidate 1 shipped on its own, unrelated to this spec, as part of a larger move of
`AbstractProvider<T>` into the shared `Utility` submodule (`fde73df`,
`4347a13`) so InventoryTetris and AutoBattler consume the same singleton base. That move
split the class into `AbstractSceneSingleton<T>` (scan/create/disable-duplicates — the
generic scene-singleton contract) and `AbstractProvider<T>` (adds the
`DontDestroyOnLoad` promise, now enforced at edit time via `OnValidate` erroring on a
non-root object). The shutdown guard landed as part of that split
(`Utility` submodule commit `3010155`): a `_isQuitting` flag set in
`OnApplicationQuit()`, checked first in `Instance`'s getter, which returns the existing
(possibly already-destroyed) reference without attempting `CreateNewInstance()` — the
same fix this spec's Implementation Decisions describes below, shipped with a cleaner
seam (a base-class split) than the bolted-on internal hook this spec proposed. No
automated test covers it (`Utility/Tests/EditMode/ProviderTests.cs` doesn't touch
`OnApplicationQuit`/`_isQuitting`) — that gap is still real and is folded into
[Out of Scope](#out-of-scope) rather than re-litigated here.

Candidate 2 is untouched: `LocalPlayer.PickUpItem` (still in
`Assets/Scripts/InventorySystem/Runtime/Character/LocalPlayer.cs`, still
`Assembly-CSharp`, not moved by the submodule refactor) reaches
`InventoryProvider.Instance.{Equipment,Inventory,Stash}` exactly as described below. The
rest of this document — Solution item 1, its Implementation Decisions subsection, and
the shutdown-guard bullet under Testing Decisions — is kept as the historical record of
what was proposed and is superseded by the note above; **candidate 2 is the only part
still live and ready for `/to-tickets`.**

## Problem Statement

`AbstractProvider<T>.Instance` cannot tell "nobody has made one yet" apart from
"everything is being torn down." Unity nulls out the reference to a destroyed
`GameObject` the same way it nulls out one that was never created, so any of the 133
`Provider.Instance` call sites reached from an `OnDisable`/`OnDestroy` during Play-mode
exit re-triggers `CreateNewInstance()` and resurrects a hollow, unwired singleton mid-
shutdown — a stray `GameObject` with none of the original's serialized references,
logging errors or silently swallowing calls in the moment the Editor is tearing
everything else down.

Separately, `Instance` is also the *only* adapter every one of those 133 call sites has
ever used, so nothing varies across the seam and none of the Runtime layer above the
container core can be exercised without a live scene booting six providers in the right
order. ADR-0007 and issue #15 already closed this exact gap one layer down —
`AbstractDimensionalContainer`/`CharacterEquipment` used to reach
`CharacterProvider.Instance.Player` and `DragProvider.Instance.ReplacePackage(...)`
directly, and now take `IStatReceiver`/`ICursorSink`/`ICurrencyMinter` at construction,
proven out by `FakeStatReceiver`/`FakeCursorSink`/`FakeCurrencyMinter` in
`ContainerTestFixtures.cs`. `LocalPlayer.PickUpItem` — real game logic, not a display —
still reaches `InventoryProvider.Instance.{Equipment,Inventory,Stash}` three times and
cannot be unit-tested as a result.

## Solution

Two independent changes, from the 2026-09-05 architecture review's candidates 1 and 2:

1. ~~**Guard `AbstractProvider<T>.Instance` against Play-mode teardown.**~~ **Shipped
   independently — see [Status update](#status-update--2026-09-21).** Kept below as the
   historical record of what was proposed.

2. **Hand `LocalPlayer` its containers instead of letting it reach for them.**
   `InventoryProvider.Awake()` already constructs `Equipment`, `Inventory`, `Stash` and
   `Wallet` and already injects three interfaces into the container core per #15. It
   gains one more step: pass `Equipment`, `Inventory`, `Stash` to the `LocalPlayer`
   instance directly, so `PickUpItem` reads its own fields instead of calling
   `InventoryProvider.Instance` three times.

This is deliberately the narrow slice, not the full sweep. It does not retire
`AbstractProvider<T>` for any provider — see [Out of Scope](#out-of-scope) and
[Further Notes](#further-notes).

## User Stories

Stories 1–5 and 12 describe candidate 1 and are already satisfied (see Status update).
The rest describe candidate 2, which is still open.

1. As a player, I never want a stray `GameObject` to appear, or an error to log, in the
   moment I stop a Play session.
2. As a developer, I want `Instance` to stop resurrecting a destroyed provider during
   shutdown, so that the console stays clean when a session ends.
3. As a developer, I want the shutdown guard to live once on `AbstractProvider<T>`, so
   that all six current providers — and any future one — get the fix without knowing
   it exists.
4. As a developer, I want the "should this call create a new instance" decision to be
   testable without a live Play-mode session, so that the fix has a regression net
   instead of relying on manual reproduction.
5. As a developer, I want no behaviour change while a session is actually running — the
   guard only changes what happens during teardown.
6. As a developer, I want `LocalPlayer.PickUpItem` to be unit-testable, so that pickup
   placement (auto-equip, overflow to stash) is coverable without a scene.
7. As a developer, I want `LocalPlayer` to take its containers the same way
   `CharacterEquipment` already takes `IStatReceiver` — construction-time hand-off, not
   a lazy lookup — so that the pattern is consistent across the seam #15 opened.
8. As a maintainer, I want this slice to touch exactly one consumer
   (`LocalPlayer.PickUpItem`), so that the change is reviewable in one sitting and does
   not repeat the "big-bang" mistake ADR-0007 already rejected once for assembly
   extraction.
9. As a maintainer, I want `InventoryProvider`, `CharacterProvider`, and the other four
   providers to keep working exactly as before for every other caller, so that this
   slice carries zero risk to the GUI/drag surface it does not touch.
10. As a future contributor picking up the next slice, I want this spec to say plainly
    that `AbstractProvider<T>` is not retired here, so that the remaining ~127 call
    sites are understood as deliberately deferred, not forgotten.
11. As a developer, I want the new test for `PickUpItem` to use the same fake-container
    style as `ContainerTestFixtures.cs`, so that the testing pattern in this codebase
    stays singular.
12. As a developer, I want the shutdown-guard test to reuse the `Assembly-CSharp-Editor`
    seam issue #4 already proved, so that no new test assembly or extraction is needed
    to cover `AbstractProvider<T>` while it still lives in `Assembly-CSharp`.

## Implementation Decisions

### Guard `AbstractProvider<T>` against Play-mode teardown — shipped, historical record only

- A static `isQuitting` flag on `AbstractProvider<T>`, set by subscribing to
  `Application.quitting` once per closed generic type (guarded against a double
  subscription the same way `Instance` already guards against a double creation).
- `Instance`'s getter: once `isQuitting` is set, it skips `CreateNewInstance()` and the
  `DontDestroyOnLoad` promotion entirely, returning the existing `instance` field as-is
  (which may itself be Unity's destroyed-object fake-null) and logging once. The goal is
  "stop recreating," not "make the reference safe to dereference during shutdown" — no
  call site is expected to do meaningful work in that window.
- No change to `InstanceExists()` / duplicate-candidate handling / naming — those are
  candidate 3 from the review (out of scope here).
- The predicate itself (`isQuitting` → skip creation) is exposed to tests via an
  `internal` hook, following the existing `[InternalsVisibleTo(...)]` idiom already on
  `Package.cs` and `AbstractSlotDisplay.cs` — a test sets the flag directly rather than
  waiting for a real `Application.quitting` event.

### Hand `LocalPlayer` its containers directly

- `LocalPlayer` gains a plain setter (property or method — not a constructor;
  `MonoBehaviour`s are instantiated by Unity) taking `CharacterInventory Inventory`,
  `CharacterInventory Stash`, `CharacterEquipment Equipment`.
- `InventoryProvider.Awake()` calls it once, immediately after constructing the four
  containers — the same place issue #15 already calls out as "the natural place to hand
  out interfaces."
- `LocalPlayer.PickUpItem` reads its own injected fields; the three
  `InventoryProvider.Instance.{Equipment,Inventory,Stash}` reads it currently makes are
  deleted.
- No new interface. `CharacterInventory`/`CharacterEquipment` are already concrete,
  already-tested modules (their own behaviour is covered via #15's container fixtures)
  — this is a reference hand-off, not a port. Per `/codebase-design`'s dependency
  categories, this is in-process/local-substitutable: the real thing and the fake thing
  used in tests are both the same plain `CharacterInventory`, so no adapter is needed.
- `InventoryProvider.Awake()`'s own `CharacterProvider.Instance.Player` and
  `ItemProvider.Instance` lines are unchanged — this slice does not touch
  provider-to-provider reach-throughs (see Out of Scope).

## Testing Decisions

A good test here pins observable behaviour through the interface, never internal
state — the same discipline `MutableFloatTests`/`ContainerTestFixtures` already follow
in this codebase.

- **Shutdown guard — moot, shipped elsewhere.** The seam sketch below (an `Editor/`-folder
  test reusing issue #4's `Assembly-CSharp-Editor` finding) was written before the fix
  moved into the `Utility` submodule. It no longer applies as written — `AbstractProvider<T>`
  isn't in `Assembly-CSharp` any more — and the shipped fix still has no automated test
  (`ProviderTests.cs` doesn't cover `OnApplicationQuit`). If that gap gets closed, it
  belongs in the `Utility` submodule's own EditMode suite, not here. Original sketch,
  kept for the record: a real `GameObject` with a throwaway provider subclass, force
  `Instance` once, set the quitting flag, destroy the backing `GameObject`, assert a
  second `Instance` read creates nothing new.
- **`LocalPlayer.PickUpItem`.** A new EditMode test, same seam as above (`LocalPlayer`
  is also `Assembly-CSharp`), constructs real `CharacterInventory`/`CharacterEquipment`
  instances (no scene needed for them — they are plain C# types), calls the new setter
  on a `LocalPlayer` created via `new GameObject().AddComponent<LocalPlayer>()`, then
  calls `PickUpItem` and asserts placement: an equipment package with `autoEquip` on
  lands in `Equipment`; otherwise it lands in `Inventory`; overflow lands in `Stash`
  only in a debug build. Prior art: `ContainerTestFixtures.cs`'s fake-adapter style,
  applied to concrete types instead of interfaces since none is being introduced here.
- **Modules tested**: `AbstractProvider<T>`'s creation-guard predicate, and
  `LocalPlayer.PickUpItem`. No other provider or GUI component changes in this slice, so
  nothing else gains or loses test coverage.

## Out of Scope

- **Retiring `AbstractProvider<T>` for any of the six providers.** `InventoryProvider`,
  `CharacterProvider`, `ItemProvider`, `DragProvider`, `PreviewProvider`, and
  `SceneProvider` all keep their `Instance` singleton exactly as it works today for
  every caller except the one deleted in this slice.
- **The other ~127 `Provider.Instance` call sites** — `AbstractSlotDisplay`,
  `ResourceDisplay`, the vendor/sell slot displays, `InventoryProvider`'s own ~20
  debug-UI button methods, and every other consumer across the 34 files the review
  found. Each is a candidate for the same injection treatment, deferred to its own
  follow-on ticket per provider rather than swept in here.
- **`InventoryProvider.Awake()`'s own provider-to-provider reach-throughs**
  (`CharacterProvider.Instance.Player`, `ItemProvider.Instance`). Unwinding these is the
  same shape of change as this slice but is a separate step — see Further Notes.
- **Candidate 3 from the architecture review** — splitting `AbstractProvider<T>`'s six
  fused responsibilities (scan/create/name/`DontDestroyOnLoad`/disable-duplicates/warn)
  behind an internal seam. Speculative, not requested for this spec — and largely
  overtaken by the same submodule move noted in the Status update, which split the
  class into `AbstractSceneSingleton<T>` + `AbstractProvider<T>` for unrelated reasons
  (sharing the base with AutoBattler). Re-reading that split against candidate 3's
  framing, if anyone wants to close the loop, is optional.
- **A test for the shutdown guard.** It shipped without one (see Status update); adding
  it is a small, separate follow-up in the `Utility` submodule, not this spec.
- **Any gameplay behaviour change.** Both changes are structural; pickup placement
  rules and shutdown-time behaviour while actually playing are unchanged.

## Further Notes

### Is the singleton still the right call, once injection is in?

Raised directly during scoping: if injection is the better way for `LocalPlayer` to
reach its containers, is `Instance` still the right shape for `InventoryProvider` and
`CharacterProvider` themselves? The honest answer is that this spec's slice is a step
toward "no," not a declaration that it already is "no." Reaching an end state where
`AbstractProvider<T>` has no remaining callers means every one of the 133 sites is
rewired — including the provider-to-provider reaches
(`InventoryProvider.Awake()` finding `CharacterProvider.Instance.Player` to get the
`LocalPlayer` to inject into in the first place) that this slice deliberately leaves
alone. That is a separate, larger effort — plausibly its own spec once this slice's
shape (setter injection from a provider's `Awake` into a sibling `MonoBehaviour`) has
been proven out once. Two candidates for what unlocks it next: an explicit
`[DefaultExecutionOrder]` sequencing of the providers plus direct `[SerializeField]`
cross-references wired in the Inspector, or a single composition-root `MonoBehaviour`
that wires every provider to every other one at one `Awake()`, replacing `Instance`
lookups everywhere at once. Either way, `Instance`'s main remaining job today —
sidestepping Unity's otherwise-nondeterministic `Awake` ordering between sibling
top-level `MonoBehaviour`s — needs a replacement before the locator itself can go.

### Candidate 1 shipped independently — the prediction that mattered

This spec argued candidate 1 shouldn't wait on candidate 2, since five providers and
well over a hundred call sites would keep depending on `Instance`'s lazy-create
behaviour regardless of how far the injection sweep went. That's exactly what happened,
just not through this spec — it landed as a side effect of moving `AbstractProvider<T>`
into the `Utility` submodule for a different reason (sharing the base with AutoBattler).
The lesson generalises: a fix scoped to "one class, no behaviour change while playing"
doesn't need this spec's blessing to ship: it shipped opportunistically while touching
the class for something else.

### Why `LocalPlayer` first

Of the 133 call sites, `LocalPlayer.PickUpItem` is one of the few that is real game
logic rather than a display — the rest (`AbstractSlotDisplay`, `ResourceDisplay`, the
drag/preview surface) are scene-bound UI in the same category ADR-0007 already exempts
from unit coverage. Injecting into those buys locality, not new test coverage, which is
why they are deferred rather than folded into this slice.
