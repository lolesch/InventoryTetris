# Player session flow — what exists, what is missing, what order

Date: 2026-09-28
Status: **Research / roadmap, pre-spec.** Input for a `/grilling` + `/domain-modeling` pass,
then `/to-spec`. The owner answered section 6's open calls on 2026-09-28; the answers are
recorded there and override the recommendations in §4.

The next phase shifts focus from single mechanics to the whole experience: launch the app,
pick or create a hero, play Runs, quit, come back to the same state. This doc catalogues
what the repo already has toward that, what the specs deferred, and what is missing
entirely, then proposes an order.

---

## 1. What already exists

| Capability | State | Where |
|---|---|---|
| Item instance ↔ POCO round trip | **Built + tested** | `ItemInstance.ToDto/FromDto`, `ItemInstanceDto`, `ItemInstanceDtoTests` |
| Stable item definition id + lookup | **Built** | `ItemDefinition.Id`, `IItemCatalog` / `ItemCatalogAsset` |
| Stable Location id | **Built** | `LocationConfig.Id` (slug, never asset GUID) |
| Container DTO `[{ x, y, instance, amount }]` | Designed, **not built** | foundational-rework spec §Persistence constraints |
| `ISaveStore` (+ `FileSaveStore`, `InMemorySaveStore`) | Named, **not built** | same section; `ItemInstanceDto` doc comment |
| What the save contains | **Specified** | MVP loop spec, stories 63–67 + §Persistence constraints |
| `InTown` on launch | Built (#45) — trivially true, since nothing loads | `SimulationProvider` |
| Corpse | Built, in-memory | `Corpse` keys by `EncounterProfile` **reference** (memoized in `SimulationProvider.ProfileFor`) |
| Wallet | Built | Coins physically sit in the Inventory grid — persisting the Inventory *is* persisting the Wallet |
| Hero behaviour sliders | Built, plain storage | `HeroBehaviour` — doc comment already says "held in the Session save" |
| Hero progression | Built, ad hoc | `LocalPlayer.GainExperience` — `CharacterLevel` + `Experience` resource; each level-up *adds a `StatModifier`*, so stats are derived, not stored |
| Hero base stats | Hard-coded | `BaseCharacter` constructs its resources inline; no class / archetype data |
| Scene flow | One scene | `EditorBuildSettings` lists only `Assets/Scenes/Example.unity` |
| Menu primitives | Built (Utility) | `LoadSceneButton`, `QuitGameButton`, `SceneProvider` |
| Provider lifecycle | Partial | Every `AbstractProvider<T>` is `DontDestroyOnLoad`; Utility@`3010155` split out an opt-in `AbstractSceneSingleton<T>`; **#46 open** |
| Dev spawners | Live in the game | `InventoryProvider.AddRandomLoot / AddRandomCurrency` |
| Healer | Built (#58 still open) | `HealerAction`, `InventoryContext.Healer` |

**Not present at all:** main menu, hero creation, hero selection, pause menu, settings
screen, any file on disk, a defined starting state for a new hero.

## 2. What the docs already deferred that this phase touches

- **One hero per Session; choosing among saved heroes is deferred** — `CONTEXT.md` **Hero**.
- **The shared Stash has no owner** — #68. Triaged as "write a defer note". Character
  selection makes it due: see §3.
- **The save system itself** — MVP spec *Out of Scope*; ADR-0006 *Consequences*.
- **Stash tabs** — #70, foundational-rework Tier 3.
- **Rebindable input with persisted overrides** — #69.
- **Progressive Location unlocks** — MVP spec *Out of Scope*; `LocationConfig` leaves room.
  If it ever lands, the unlock set lives in the hero save.
- **A recap / results screen** — ruled out by ADR-0008. Not revisited here.

## 3. Why character selection changes the prerequisites

Three things that were safe to defer stop being deferrable the moment there is more than
one hero:

1. **#68 — a tier above the hero.** `CONTEXT.md` says the **Session** owns "the hero, the
   four containers, the wallet and XP". With several heroes, the save outlives any one of
   them. If the Stash stays shared between heroes (its original description), it cannot sit
   inside a hero. #68 warned exactly this: "serialise the Session, which owns the four
   containers" silently forecloses the shared Stash.
2. **"Session" means three things.** Today it is the play span (app start → quit), the
   persistence unit, *and* the owner of the hero. With selection those split: the play span
   still exists, but the persistence unit becomes the new tier, and the hero becomes a
   thing it holds several of. The glossary needs the split before any DTO is named.
3. **#46 — providers that survive a scene load.** A menu → game transition, or switching
   heroes, either reloads the game scene or resets it in place. Today every provider is
   `DontDestroyOnLoad`, so `InventoryProvider` would carry the previous hero's containers
   into the next load. Scene-scoped providers (the opt-in persistence #46 already
   proposes) are the clean fix: the game scene rebuilds, then hydrates from the selected
   hero.

## 4. Mandatory additions no document covers yet

**A1 — Ownership table.** Which state belongs to which tier. Proposed:

| State | Owner | Note |
|---|---|---|
| Heroes list, last-selected hero | Account | |
| Stash | hero | hero-scoped first (§6.1); a shared tab owned by the Account comes with Stash tabs |
| Inventory, Equipment | hero | |
| Wallet | hero | coins sit in the bag; coins put into the Stash are already shared because they are items — a separate shared bank is not needed |
| Level, XP, current Health / Resource | hero | save current values so quitting after a Death does not heal |
| `HeroBehaviour` sliders, selected Location | hero | |
| Corpse | hero | persist as Location **id** + instance DTOs |
| Supply | **not saved** | Restock on load and on every Recall (§6.4) |
| Sell Basket, hand/cursor Package, ground Drops, the Run | **never saved** | see A2 |
| Settings, key bindings | machine / Account | separate file from hero data |

**A2 — Save lifecycle rules.**
- **Write points:** every `InField → InTown` transition (Recall and Death), exit to menu,
  hero switch, app quit. Not on a timer.
- **Quit mid-Run** runs the implicit Recall *first*, then writes. That reconciles "the save
  is only ever written `InTown`" (story 67) with "quitting banks what the hero holds"
  (story 65).
- **Pre-save normalisation:** cancel a staged sale and return a held Package through
  **Return to Origin** before snapshotting. Otherwise a Package in the Sell Basket or on the
  cursor at quit is lost. The save format never describes either.
- **Atomic write:** temp file + rename, keep one `.bak`.
- **Schema version** field from the first write, with a migration hook (even if empty).
- **Load tolerance:** an unknown `definitionId` or Location id is quarantined and logged,
  not thrown. Item content churns in a prototype; a renamed definition must not brick a save.
  (`ItemInstance.FromDto` currently fails loud on an unparseable enum — keep that for enums,
  decide separately for missing definitions.)

**A3 — Hero stats derived from level.** Loading must not replay `GainExperience` N times to
rebuild the per-level `StatModifier`s. Extract a pure "modifiers for level N" function the
level-up path and the loader both call.

**A4 — Location lookup by id.** The Corpse is keyed by profile *reference*. Loading needs an
id → `LocationConfig` catalog (mirroring `IItemCatalog`), then the memoized profile.

**A5 — New-hero starting state.** Level 1, what in the bag, what equipped, how many coins.
Today the start is whatever the scene and the debug spawners produce.

**A6 — Dev tools out of the player path.** Gate `AddRandomLoot` / `AddRandomCurrency` (and
any other debug buttons) behind a dev panel or scripting define; add a "wipe save" dev
command.

**A7 — Scene / flow architecture.** Proposed: `MainMenu` scene (Continue · Heroes · Settings
· Quit) → `Game` scene. A persistent save service (the one legitimately
`DontDestroyOnLoad` object) holds the loaded save and the selected hero id; game-scene
providers are scene-scoped and hydrate from it on load. Depends on #46.

**A8 — Escape ownership.** `DragProvider` already consumes Escape to cancel a drag; a pause
menu wants it too. Needs a precedence rule — cancel drag → cancel staged sale / close side
panel → open pause menu. Either #69 lands first, or the pause-menu spec defines a small
Escape stack that #69 later absorbs.

## 5. Proposed order

### Phase A — close out the open slice (no session work)

| # | Issue | Why here | Note |
|---|---|---|---|
| 1 | #86 → #87 | Finishes the Inventory Context epic #81 | #87 is docs-only, last |
| 2 | #93 → #94 | Finishes the Combat Panel | #94 needs the resource types #93 moves |
| 3 | #63 Ground items | Drops and Corpse overflow are invisible without it | Its blocker ("LootFlow merged") is satisfied. Its "shift-quick-move to the floor" criterion must become a row in #86's context table — **refresh the ticket first** |
| 4 | #58 Healer | **Already built, issue still open** | `HealerAction` (`5587306`, `af216c6`) refills on entry into `InventoryContext.Healer`; the scene has `HealerToggle` with hotkey `H`. Its body still describes the `RadioGroup` / `TownGroup` mechanism #74/#85 replaced. Check the visual-feedback criterion, then close |
| — | #70 | Stays a parking lot | Pull the item-comparison bug out as its own ticket when polish comes up |

### Phase B — decide (no code)

1. `/grilling` over §3, §4 and §6.
2. `/domain-modeling`: add **Account** (§6.2), split **Session**, add **New Hero**, **Main
   Menu**, **Hero Select**, **Pause Menu** entries; ADR-0013 for the ownership table. This
   **closes #68** with a real decision rather than a defer note.
3. `/to-spec` → `dev/specs/YYYY-MM-DD-save-system-design.md` and
   `…-front-end-flow-design.md` (or one spec, if the grilling finds them inseparable).
4. #46's architecture pass, since A7 depends on its outcome.

### Phase C — persistence core (engine-free, TDD)

Prior art from the sibling repos and the submodule proposal:
`2026-09-28-save-serialization-prior-art.md`.

0. `Utility.Persistence` in the submodule — `ISaveStore`, `FileSaveStore` (atomic, `.bak`),
   serializer adapter, versioned envelope, `SaveSlot<T>`. Absorbs item 5 below.
1. Container DTO round trip — Inventory, Stash, Equipment (slot positions included).
2. Level → stat modifiers as a pure function (A3).
3. Location catalog by id; Corpse DTO (A4).
4. Hero DTO and Account DTO; the schema version rides on the submodule's envelope.
5. *(moved to step 0)*
6. Load-tolerance policy (A2) — in the game-side mappers, where `IItemCatalog` is in reach.

### Phase D — wire it into the running game

1. #46 — scene-scoped providers.
2. Snapshot / hydrate adapters, with pre-save normalisation (A2).
3. Write points: Run end, quit (implicit Recall first), exit to menu.
4. New-hero starting state (A5); gate the dev spawners (A6).

At the end of D the single-hero game survives a restart — the first playable milestone of
this phase, before any menu exists.

### Phase E — front end

1. `MainMenu` scene: Continue, Settings, Quit (A7).
2. Hero select + create (name only) + delete (with confirm).
3. Pause menu with Save & Exit to Menu (A8).
4. Settings screen; #69 input service with persisted bindings.

### Phase F — unlocked by the above, not required for the experience

Stash tabs (#70) on the shared Stash · hero classes / archetypes (needs attribute design;
the biggest open design item here) · Healer side panel · progressive Location unlocks ·
multiple save profiles.

## 6. Owner decisions (2026-09-28)

1. **Stash scope — hero-scoped first.** The end state is **Stash tabs**: one tab shared
   among heroes, the rest local to a hero. The first build ships a single hero-scoped Stash,
   so the Stash row in A1 moves to **hero**. The shared tab arrives with Stash tabs (Phase
   F) and is then owned by the Account. #68 closes on this answer: its three questions are
   answered as "hero-scoped now, one shared tab later, the Wallet stays with the hero".
2. **The tier above the hero is the `Account`.** It owns the hero list, the last-selected
   hero and, later, the shared Stash tab. It needs a `CONTEXT.md` entry and an *Avoid* list
   (*profile*, *save file*, *user*).
3. **Character selection is name only.** No class or archetype design yet.
4. **The Supply is not saved. It Restocks on load and on every Recall.** Today it stays
   until a manual Restock; that manual button goes away later. Still open for the spec:
   whether a Death, which also returns the hero to Town, Restocks too.
5. **Two scenes** (`MainMenu` → `Game`). The menu scene has already been started locally
   but is not committed anywhere yet. Pick it up from there rather than starting fresh.
   #46 stays a prerequisite (A7).
