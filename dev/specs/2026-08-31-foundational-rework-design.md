# Foundational rework — the item model, the transaction seam, the wallet

Date: 2026-08-31
Status: Scoping spec — cut implementation plans from this; it is not itself a plan.
Base: to be cut after `feature/probability-distribution-rebuild` lands — the item
generator leans on `ProbabilityTable<T>`.

Supersedes the ordering in `dev/specs/2026-08-30-item-movement-model-design.md`: that
spec's `ItemTransaction` work now lands **after** the item-model split, not before.
Everything else in that spec still stands and is folded in here as Phase 2.

## Why this exists

A pass over the whole inventory / item / loot / economy surface, measured against what
an ARPG's inventory-management scope needs. The finding is structural, not a list of
features: **three missing seams sit under almost every unfinished thing**, and until
they exist, each new feature pays their tax again. This spec captures the assessment
and designs the three seams. The feature backlog they unblock is catalogued in
[Out of Scope](#out-of-scope--the-backlog-these-seams-unblock) so it is not lost.

### Where the code is

Depth read: a module is **deep** when a lot of behaviour sits behind a small
interface; **shallow** when the interface is nearly as wide as the implementation.

| Cluster | Modules | Read |
| --- | --- | --- |
| Spatial container | `AbstractDimensionalContainer`, `CharacterInventory`, `CharacterEquipment` | Medium behaviour (grid packing, stacking, swap, 2H double-slot, `Sort`) behind a **wide, leaky interface**: the `ref Package` mutation protocol, `AddAtPosition` returning the displaced package, callers needing to know the 0/1/2-overlap rule and to call `InvokeRefresh()` themselves. Packing lives only in `CharacterInventory` because the base leaves `AddAtPosition` / `GetStoredItemsAt` abstract (`AbstractDimensionalContainer.cs:86,101`). |
| Wallet | *none — methods on `CharacterInventory`* | `TryPay`, `CanAfford`, `CalculateCash`, `Consolidate`, `RemoveCurrency`, `AddChange` (`CharacterInventory.cs:102-244`) all scan `StoredPackages` for `CurrencyItem`. Delete "the wallet" and coin-scanning loops reappear in vendor code — it has earned a module and does not have one. |
| Currency value | `Currency`, `CurrencyDropRoll` | **Deep.** Pure, Unity-free, unit-tested, small interface. A template. |
| Item | `AbstractItem` + `EquipmentItem` / `ConsumableItem` / `CurrencyItem`, `ItemTypeData`, `AbstractItemObject` | **Shallow data bag with an inverted constructor.** `new EquipmentItem(type, rarity)` *is* the loot roll — the constructor calls `ItemProvider.Instance` for icons, affix pools and uniques (`AbstractItem.cs:237,246,300`). You cannot describe an item without generating one, or build one in a test. Behaviour (`GetRarityColor`, `GetDimensions`, `CalculateValue`) is scattered static switches; `Equals` ignores affixes (`AbstractItem.cs:112`). |
| Loot generation | `ItemProvider`, `AbstractProbabilityDistribution`, ~11 distribution SOs, `CurrencyDropTable` | `ItemProvider` is **~30 `GenerateRandomX` methods, each a one-line switch** — a hand-unrolled decision tree, very wide interface, pass-through body. `AbstractProbabilityDistribution` is mid-rebuild behind a testable `ProbabilityTable<T>` seam (`feature/probability-distribution-rebuild`). |
| Stats | `MutableFloat`, `CharacterStat`, `CharacterResource` | **Deep and tested.** `MutableFloat` is sealed, cached, event-on-change, no override chain. The other two are thin composition adapters. A template. |
| Combat | `BaseCharacter`, `BaseCharacterExtensions`, `CharacterProvider`, `DummyTarget` | **A debug harness, not a system.** `DealDamageTo` / `ReceiveDamageFrom` fire once per button press; regen is `async void` + `Task.Delay` in `Update()` (`BaseCharacter.cs:64`); `// TODO: COMBAT TICK RATE` (`:52`); formulas are "just for testing" (`BaseCharacterExtensions.cs:17`). No attack loop, skills, cooldowns, targeting, crit, death→respawn, encounter. |
| Drag / UI | `AbstractSlotDisplay`, container displays, `DragProvider`, `PreviewProvider`, `InventoryProvider` | Drag geometry extracted + tested (`DragGeometry`). But each slot display reimplements `MoveItem` / `DropItem` with duplicated remove/add/coin-mint logic — `VendorSlotDisplay.DropItem` and `SellItenSlotDisplay.DropItem` are near-identical ~20-line blocks. `InventoryProvider` is a god object: 4 containers + ~40 UI callbacks + restock. |
| Persistence | *none* | No save/load anywhere — no `PlayerPrefs`, JSON, or disk. Containers are `new()`'d in `InventoryProvider.Awake()` every run (`InventoryProvider.cs:52-61`). `[Serializable]` is inspector-only. |

### What the ARPG inventory-management scope needs that isn't here

**Have:** grid backpack; equipment paperdoll (14 slots, 2H, dual-wield, ring×2);
gold/currency (strong, recently rebuilt); comparison tooltips (`PreviewProvider`
compares vs equipped); `Sort()`; stack merge; a minimal vendor with markup; a `Stash`
container + bulk stash-all.

| Missing / stubbed | Today | Blocked on |
| --- | --- | --- |
| **Persistence** | nothing survives a scene load, let alone a quit | Seam 3 |
| **Ground loot** | `DummyTarget.OnDeath` calls `PickUpItem` directly — items vacuum into the bag (`DummyTarget.cs:18-22`, `//rework to drop items on the floor`) | Seam 2 |
| **Transactional movement** | multi-step moves lose items; live `StackOverflowException` + item deletion on 2H-over-bow+shield (QA-4) | Seam 2 |
| **Item level / power budget** | no concept; loot does not scale with source | Seam 1 |
| **Crafting / item modification** | none — `Crafted` / `Set` / `Uncommon` commented out in `ItemRarity.cs` | Seams 1 + 2 |
| **Sockets & gems / runes** | none | Seam 1 |
| **Identification** | none | Seam 1 |
| **Item requirements** | empty `#region REQUIREMENTS` in `AbstractItem.cs:193` | Seam 1 (+ item level, attributes) |
| **Consumable effects + potion belt** | right-click just deletes the item (`InventorySlotDisplay.cs:84-89`) | Seam 1 |
| **Affix system depth** | no prefix/suffix, no tiers, no pool weighting (`ItemTypeData.cs:117` `// TODO: AllowedStats distribution`), no guaranteed type stats (`:86`), uniques picked uniformly ignoring level (`ItemProvider.cs:387`) | Seam 1 |
| **Per-source loot tables** | one global table | Seam 1 |
| **Stash tabs, shared stash, alts** | one `Stash`, no tabs | Seams 1 + 3 |
| **Vendor depth** | one `Store` (a `CharacterInventory`), `Markup = 1.5f`, no buyback / repair / gamble / identify / refresh | Seams 1 + 2 |
| **Stack-split UI, context menus, item lock, drag-cancel/refund** | `// TODO: split` (`InventorySlotDisplay.cs:106`); `DragProvider` `ReturnToOrigin` stubs commented (`:232-250`); vendor drag charges then can lose the item | Seam 2 |
| Charms / passive-inventory items, quest items, item sets, loot filter, dimension-based value | none | Seam 1 |

### The through-line — three missing seams

1. **The item is not separable from the item roll.** `AbstractItem`'s subtype
   constructors reach into a singleton and perform the whole roll. There is no
   *template* ("what a Rare Chest can be") distinct from a *rolled instance* ("this
   chest, these affixes"). Crafting needs to mutate an instance; sockets and
   identification need per-instance state; item level needs a roll context; save/load
   needs a serializable instance with a stable template reference; a trustworthy
   `SellValue` needs the affix model pinned down. All the same seam.

2. **Item movement has no transactional guarantee.** A move is `remove()` then
   `add()` across the container interface at every call site, with no rollback — hence
   the QA-4 crash-and-lose-an-item and the vendor "charged me, item's gone" gap.
   Fully designed already in `2026-08-30-item-movement-model-design.md`.

3. **Nothing persists.** The fight → town → manage-storage → fight loop the project is
   heading toward is meaningless if state does not survive a session. Retrofitting
   serialization onto a grown item model, wallet, and container set is far more
   expensive than honouring the constraint from the start.

**Decision (2026-08-31):** do the item split first (Seam 1), then the transaction
(Seam 2), then the wallet; bake Seam 3's constraints into Seam 1 now and build the
save system later. Rationale in [Further Notes](#sequencing-rationale).

## The rework, in four phases

Each phase lands on green — compiles with zero console errors *and*
`Test Runner ▸ EditMode ▸ Run All` passes. Compile-check via the unity-mcp bridge
(`CompilationPipeline.RequestScriptCompilation`, then `Unity_GetConsoleLogs`);
`dotnet build` lies here (stale `.csproj`). A human runs `Run All` at each gate.

| Phase | Deliverable | Green gate |
| --- | --- | --- |
| 0 | Test seam + shared value-type carve-out | container core and item types reachable from an EditMode test assembly; `Data.Tests`, `Geometry.Tests`, `Probability.Tests` still green; **zero behaviour change** |
| 1 | `ItemDefinition` / `ItemInstance` / `ItemGenerator` / catalog; every call site migrated; persistence constraints in place | item-model tests green (roll shape, stacking identity, DTO round-trip); in-editor smoke: generate loot, equip, sell, buy, sort, preview all still work |
| 2 | `ItemTransaction`; every move path routed through it; the movement matrix | `2026-08-30-item-movement-model-design.md` Phase 1–3 gates, on the new item model |
| 3 | `Wallet` module; vendor/sell paths thinned | wallet tests green (pay smallest-first, consolidate value-preserving); currency still renders in grid cells; buy/sell conserve value |

## User Stories

1. As a developer, I want to construct an item in a test without a scene or a
   singleton, so that item behaviour is coverable at all.
2. As a developer, I want the loot roll to take a context (source level, magic find)
   as a parameter, so that per-source scaling and drop tables have somewhere to plug
   in.
3. As a developer, I want one primitive every item move goes through, so that "can
   this operation lose an item" is answered and tested in one place.
4. As a maintainer, I want an item's rolled state to serialize to a plain object with
   a stable template id, so that adding save/load later is not a second rewrite of the
   item model.
5. As a maintainer, I want the ~30-method `ItemProvider` surface to collapse to a
   catalog query plus a generator call, so that adding an item type is data, not code.
6. As a maintainer, I want currency logic out of `CharacterInventory`, so that a
   vendor container and a stash do not inherit a wallet they never use.
7. As a loot designer, I want to author a base item type and its affix pool in one
   place, and a unique as the same type with a fixed affix list, so that the two are
   not different code paths.
8. As a player, when I equip a two-hander over a weapon + off-hand, I want both
   displaced items to reach my inventory or the swap not to happen — never to lose
   gear.
9. As a player, I never want the game to freeze or throw while I rearrange equipment.
10. As a player, I want selling, buying, dropping, and auto-sort to conserve every
    item and coin.
11. As a player, I want the red "can't drop" tint to appear exactly when the drop
    would be refused, on every slot type.
12. As a player, I want my stash, bag, gear, wallet and character to be there when I
    come back — *(delivered later; the seam is built now)*.
13. As a developer, I want crafting, sockets, identification and item requirements to
    each be a small addition to one item model, not a fork of three constructors.
14. As a designer, I want `SellValue` to rest on a pinned affix model rather than a
    switch of guessed ratios, so that economy numbers can be measured.
15. As a developer, I want the pure item core in its own assembly with no Unity
    dependency in the roll path, so that it follows the same shape as `MutableFloat`,
    `Currency`, and `ProbabilityTable`.

## Phase 0 — the test seam and the value-type carve-out

The container core (`AbstractDimensionalContainer`, `CharacterInventory`,
`CharacterEquipment`), `Package`, `AbstractItem` and its subtypes, and every enum
under `Data/Enums/` live in the predefined `Assembly-CSharp`, which an asmdef test
assembly cannot reference. The dead
`[assembly: InternalsVisibleTo("InventorySystem.Data.Tests")]` on `Package.cs:7` and
`AbstractSlotDisplay.cs:12` shows this has been wanted before.

Phase 0 does two things:

### The shared value-type carve-out

Move into `InventorySystem.Data` (or a sibling `InventorySystem.Enums` it references),
**namespaces unchanged** so no call site changes — only file locations and `.meta`
GUIDs move, exactly as the MutableFloat port moved `StatModifier`:

- `Data/Enums/*` — `StatName`, `ItemRarity`, `ItemCategory`, `EquipmentType`,
  `EquipmentCategory`, `WeaponCategory`, `ConsumableType`, `CurrencyType`, `ItemSize`,
  `DamageType`.
- `Data/Structs/CharacterStatModifier.cs`.

The probability rebuild deliberately kept its generic table enum-agnostic to *avoid*
this move. The item model cannot avoid it — affixes are `CharacterStatModifier`,
rarity is `ItemRarity`, footprint is `ItemSize`. This is the largest mechanical piece
of the phase; it is low-risk (GUIDs travel with `.meta` files) but touches many files'
physical paths.

### The container test seam

A timeboxed spike (half a day) with a committed fallback, per
`2026-08-30-item-movement-model-design.md` Phase 0. Given the item model is being
extracted into its own assembly regardless, the fallback (extract the container core
into `InventorySystem.Containers`, break the `CharacterProvider` / `DragProvider`
singleton coupling with injected interfaces or events) is the likely path and is the
cleaner one anyway. Record the outcome in the Phase 0 plan section.

**Gate:** a throwaway `[Test]` that news up a `CharacterInventory`, adds a package,
asserts on `StoredPackages`, green in `Run All`; existing test assemblies unchanged;
zero behaviour change.

## Phase 1 — the item model split

### The seam: `ItemDefinition` vs `ItemInstance`

A new assembly `InventorySystem.Items`, referencing `InventorySystem.Data` and
`InventorySystem.Probability`. The roll path is Unity-free; only the authored-content
adapter touches `UnityEngine`.

- **`ItemDefinition`** — the immutable template. An interface (the facts the
  generator, the instance, and the displays need), so tests pass a fake and no
  `ScriptableObject` is instantiated in a test — the rule the probability rebuild
  already follows.

  ```
  string        Id                  // stable; survives rename/move (see persistence)
  ItemCategory  Category             // Equipment | Consumable | Currency
  ItemSize      Footprint
  uint          BaseStackLimit
  IReadOnlyList<AffixSlot> AffixPool // allowed stats + ranges + roll weighting  (was ItemTypeData)
  IReadOnlyList<CharacterStatModifier> ImplicitStats   // guaranteed, pre-roll
  ItemRequirement Requirement        // level, attributes  (fills the empty #region)
  bool          IsUnique
  IReadOnlyList<CharacterStatModifier> UniqueAffixes    // fixed, merged when IsUnique
  // Category-specific, null/None when N/A:
  EquipmentType EquipmentType
  ConsumableType ConsumableType
  CurrencyType  CurrencyType
  // Art is on the adapter, not the interface — the roll path never needs a Sprite.
  ```

  **`ItemDefinitionAsset : ScriptableObject`** is the authored adapter (holds one
  definition + the `Sprite`s). A unique is an `ItemDefinitionAsset` with
  `IsUnique = true` and a filled `UniqueAffixes` — the same type as a base item, not a
  separate `AbstractItemObject` hierarchy.

- **`ItemInstance`** — the rolled thing, immutable after construction. This is what
  `Package.Item` becomes and what serializes.

  ```
  string        DefinitionId
  ItemRarity    Rarity
  int           ItemLevel
  IReadOnlyList<CharacterStatModifier> Affixes   // implicit + rolled + unique, combined
  // later, additively: IReadOnlyList<Socket> Sockets; bool Identified; Quality
  ```

  Mutation operations (crafting, socketing, identifying) return a **new**
  `ItemInstance`; nothing mutates one in place. This keeps the Phase 2 transaction
  snapshot sound — a rolled-back craft cannot leak through a shared reference.

  Runtime-only reads the displays need (`Icon`, `Dimensions`, `StackLimit`,
  `DisplayName`, rarity colour) resolve through the catalog:
  `instance` + `catalog.Definition(instance.DefinitionId)` → the value. Wrap this in
  an `ItemView` helper so call sites stay terse; the static switches on `AbstractItem`
  (`GetRarityColor`, `GetDimensions`) move onto it.

### The generator

`ItemGenerator` replaces the `GenerateRandomX` tree:

```
ItemInstance Roll(RollContext ctx)
IReadOnlyList<ItemInstance> RollLoot(RollContext ctx, int count)

RollContext { int SourceLevel; float MagicFind; LootTable Table; }
```

Internally: pick a category (distribution SO), pick a definition from the catalog for
that category (weighted — replaces the hand-written switches), roll rarity via the
`ProbabilityTable` cascade with `ctx.MagicFind`, roll affix count and affixes from the
definition's pool, merge `UniqueAffixes` when `IsUnique`, combine same-stat modifiers
(`CombineAffixesOfSameTypeAndMod` moves here). No singleton; `RollContext` and the
catalog are parameters.

`ItemProvider` (the MonoBehaviour) becomes a thin Unity entry point owning the catalog
asset and the distribution SOs, delegating to `ItemGenerator`. Its ~30 public methods
collapse to `RollLoot` plus a handful of typed helpers the debug UI needs.

### The catalog

`IItemCatalog { ItemDefinition Definition(string id); IEnumerable<ItemDefinition> OfCategory(ItemCategory c); }`.
Real adapter: `ItemCatalogAsset : ScriptableObject` aggregating every
`ItemDefinitionAsset` (replaces the ~20 `List<AbstractItemObject>` fields on
`ItemProvider`). Test adapter: an in-memory dictionary. Two adapters — a real seam.

### Persistence constraints, baked in now (Seam 3, design-only)

The save system is not built in this rework. What Phase 1 **must** honour so it is not
a second rewrite:

- `ItemDefinition.Id` is an **explicit serialized string** (GUID or slug), not the
  Unity asset GUID (which regenerates if a `.meta` is lost) and not the asset name.
- `ItemInstance` serializes to a **POCO DTO** with no Unity types:
  `{ definitionId, rarity, itemLevel, affixes: [{ stat, value, type, rangeMin, rangeMax }] }`.
  A `ItemInstance.ToDto()` / `FromDto()` pair with a round-trip test is the whole
  deliverable — `new instance → DTO → instance` is equal.
- A container's state must be expressible as
  `[{ x, y, definitionId + instance DTO, amount }]`. `Package.Sender` is a runtime
  backref and is never serialized.
- The store seam is named but not implemented:
  `ISaveStore { void Write(string key, string json); bool TryRead(string key, out string json); }`
  with a future `FileSaveStore` and a test `InMemorySaveStore` — two adapters, so the
  seam is real when it is built.

### What happens to `AbstractItem` and its subclasses

Deleted. `EquipmentItem` / `ConsumableItem` / `CurrencyItem` were the roll — that
moves to `ItemGenerator`. The type distinction (`is EquipmentItem`) becomes
`definition.Category` and the category-specific fields on the definition. No
`ItemInstance` subtypes.

`AbstractItem.Equals` ignoring affixes (`:112`) is not a bug to carry forward:
equipment never stacks, so the affix comparison never mattered; stacking identity
below makes it explicit.

### `Package.Item` changes type

`AbstractItem` → `ItemInstance`. `Package` stays a struct
`{ AbstractDimensionalContainer Sender, ItemInstance Item, uint Amount }`.
`SpaceLeft => View(Item).StackLimit - Amount`.

### Stacking identity

Two instances stack iff
`a.DefinitionId == b.DefinitionId && view.StackLimit > 1 && !a.HasInstanceState`,
where `HasInstanceState` is false for currency and plain consumables and true for
anything with rolled affixes, sockets, or an identified flag. Equipment
(`StackLimit == 1`) never reaches the check.

### Currency

Currency stays an `ItemInstance` (definition = the coin type, no affixes, stack limit
from the definition) so coins still render in grid cells. `Currency` the struct stays
the arithmetic value type. Phase 3's `Wallet` becomes the authority on *balance*; the
grid coins are its rendering.

### The uniques migration

There are ~100+ authored `.asset` files under
`Data/Items/Uniques/**` (`EquipmentObject` / `ConsumableObject` holding a serialized
`EquipmentItem` / `ConsumableItem`). Each must become an `ItemDefinitionAsset` with
`IsUnique` + `UniqueAffixes`. Options, decided at planning time:

- an `ISerializationCallbackReceiver` / editor migration script that reads the old
  serialized affix list and writes the new shape — preferred, ~100 assets is too many
  to re-author by hand;
- accept re-authoring if the old data is judged not worth keeping (the affix values
  are hand-picked, so probably it is).

This is the single largest content-risk in the rework. Size the migration script
before committing the phase.

### Call sites (non-exhaustive; the migration surface)

`Package.cs:26,30` · `AbstractDimensionalContainer` `TryStack:61`, `Sort:211-219`,
`RemoveFromContainer:136` · `CharacterInventory` `CalculateCash:184`,
`RemoveCurrency:138` · `CharacterEquipment:21,29,142` ·
`LocalPlayer.PickUpItem:149`, `AddItemStats` / `RemoveItemStats` (iterate `Affixes`) ·
`InventorySlotDisplay.MoveItem:82-102` · `EquipmentSlotDisplay:21-23` ·
`VendorSlotDisplay` / `SellItenSlotDisplay` (`.SellValue`, `GenerateCurrency`) ·
`PreviewProvider:69-71` · `PreviewDisplay`, `AbstractSlotDisplay.RefreshSlotDisplay:267` ·
`DragProvider:77,222` · `DummyTarget.OnDeath:18` · `ItemTypeData` (folds into the
affix pool) · `EquipmentContainerDisplay:35` (`DebugItem`).

## Phase 2 — the transaction seam

`2026-08-30-item-movement-model-design.md` in full, with these deltas from doing it
second:

- Its Phase 0 (test seam) is **already done** — this spec's Phase 0 delivered it.
- The container core is almost certainly extracted (`InventorySystem.Containers`),
  since Phase 1 forced the item extraction; the item-movement spec's "spike succeeds,
  stay in `Assembly-CSharp`" branch is off the table.
- `ItemTransaction` lives in `InventorySystem.Containers` and depends on
  `InventorySystem.Items` (for `ItemInstance`) but on no provider.
- The snapshot (`new Dictionary<Vector2Int, Package>(source)`) is sound because
  `ItemInstance` is immutable (Phase 1 decision) — a rolled-back move cannot mutate a
  shared instance.
- The movement matrix, QA-2/3/4 fixes, and the displaced-item landing order are
  unchanged.

## Phase 3 — the wallet

`Wallet` (in `InventorySystem.Containers` or beside it):

```
Currency Balance { get; }
event Action<Currency> OnBalanceChanged;
bool TryPay(Currency price);          // was CharacterInventory.TryPay
void Deposit(Currency amount);        // was the ~20-line coin-mint block, ×2
void Consolidate();                   // was CharacterInventory.Consolidate
```

Backed by a container for the visible coins, but the wallet is the authority on
balance. `CharacterInventory.TryPay` / `CanAfford` / `CalculateCash` /
`RemoveCurrency` / `AddChange` / `Consolidate` (`:102-244`) move here.
`VendorSlotDisplay.DropItem` and `SellItenSlotDisplay.DropItem` lose their duplicated
coin-mint blocks and call `wallet.Deposit(currency)`. Deposit / withdraw during a buy
or sell are commit-time effects on the Phase 2 transaction.

`AutoConsolidate` (`AbstractDimensionalContainer.cs:25`) stays the container's concern
(a stash overrides it `=> true`); the wallet exposes `Consolidate()` for the manual
button.

## Testing Decisions

Prior art: `MutableFloatTests.cs`, `CurrencyTests.cs`, `CurrencyDropRollTests.cs` —
`[TestFixture] sealed class`, `Subject_Condition_Expectation` names, one behaviour per
`[Test]`, a small local builder. Assert on external behaviour, never on internal
representation (not `StoredPackages` identity, not dictionary order, not event counts).

New `InventorySystem.Items.Tests` (its own asmdef, mirroring the `Statistics` layout),
test-local fake definitions and catalog — no `ScriptableObject`, no scene.

- **Generator.** Magic find 0 reproduces the authored rarity table (shared invariant
  with the probability rebuild). Affix count matches the rarity → count map. A rolled
  affix is drawn from the definition's pool and never duplicated. `IsUnique` merges
  the fixed affixes. `RollLoot(ctx, n)` returns `n` instances (bonus-drop maths
  aside).
- **Stacking identity.** Same-definition currency stacks; two equipment instances
  never stack; a consumable with rolled affixes does not stack with a plain one.
- **DTO round-trip.** `instance → ToDto → FromDto` is equal, across rarities, affix
  counts, and item levels. A definition id survives.
- **View resolution.** `ItemView` returns the definition's footprint / stack limit /
  rarity colour for an instance; a missing definition id fails loud, not silent.
- **Wallet.** `TryPay` spends smallest-denomination-first and never overpays by more
  than one coin of the smallest it broke into; `Consolidate` is value-preserving
  (`Balance.Total` before == after); `Deposit` then `TryPay` the same amount nets to
  the start balance.
- **Phase 2** brings the movement matrix and the QA regression pins from the
  item-movement spec.

## Out of Scope — the backlog these seams unblock

Not this rework. Catalogued so the sequencing argument is auditable; each notes what
it will draw from the foundations.

### Tier 2 — the combat simulation cluster (the stated direction)

- **`Encounter` module** — `StartEncounter(config) → EncounterResult` hiding a
  fixed-timestep combat tick (replaces the `async void` regen and
  `// TODO: COMBAT TICK RATE`), enemy waves, hero auto-attack with attack-speed
  timing, death/flee outcomes. The current `BaseCharacterExtensions` formulas are
  placeholder and get replaced behind this interface. Produces loot via
  `ItemGenerator.RollLoot` with a real `RollContext`.
- **`RunState` machine** — `InTown ↔ InRun(encounter…) → ReturnToTown(loot) | Death`.
  The module that makes inventory management *matter*. Its `EncounterResult` shape and
  what it must persist both constrain Seam 3 — sketch its interface before building
  the save system.
- **Ground loot pile** — encounter output lands at a location; player picks up.
  Rides on Seam 2. Replaces `DummyTarget.OnDeath`'s auto-vacuum.

### Tier 3 — inventory & economy features (each cheap once the seams exist)

Stash tabs + an `AutoConsolidate => true` stash · crafting bench (`ItemInstance`
mutation ops + Seam 2; re-enable `Crafted`) · sockets & gems (instance state; gems are
a definition category) · identification (instance flag + a scroll consumable or
vendor) · consumable effects + potion belt · vendor depth (buyback, repair, gamble
leaning on `ItemGenerator`, currency-exchange NPC — `2026-08-26-shop-currency-followups.md`) ·
item requirements enforced on equip (`ItemRequirement` + character attributes) ·
stack-split UI · dedicated vendor container (`shop-currency-followups.md` §1) ·
drag-cancel / return-to-origin + purchase refund (`§2`; Seam 2 is its foundation).

### Tier 4 — depth cleanups

Collapse any residual `ItemProvider` switch surface once the catalog is data ·
ground the `goldRatio` switch in a combat/progression model
(`2026-08-31-item-value-open-questions.md` §1 — follows Tier 2, needs the combat
model) · push the packing implementation down into `AbstractDimensionalContainer` so
inventory / vendor / stash all go thin (`shop-currency-followups.md` §1) · larger 2H
footprints and dimension-based value (`item-value-open-questions.md` §2–3) ·
`InventoryProvider` god-object split (`InventoryProvider.cs:14`).

## Further Notes

### Sequencing rationale

Seam 1 before Seam 2 (the reverse of the item-movement spec's assumption):

- **The item extraction is the bigger, riskier change; take it while the model is
  fresh.** Doing Seam 2 first means routing every `DropItem` through `ItemTransaction`,
  then Seam 1 re-touches those same methods to change `Package.Item`'s type and swap
  `is EquipmentItem` for `definition.Category`. Front-loading the model change means
  Seam 2 is written once, against the final shape.
- **Seam 2 is already fully designed** and does not rot while it waits — the QA-4
  crash is contained (a specific equipment-swap sequence) and known.
- **Seam 3's constraints only bind Seam 1.** Deciding "instances are immutable POCOs
  with stable definition ids" while designing the model costs nothing extra now and
  removes a rewrite later; it also happens to be what keeps the Seam 2 snapshot sound.
- Phase 0's test seam is shared, so it is done once regardless of order.

Cost accepted: the QA-4 crash lives until Phase 2. If that proves painful in
play-testing, the minimal `CharacterEquipment.cs:32` `TryGetValue` fix and the
`TrySwap` give-up condition can be lifted out of the item-movement spec and applied
early as a stopgap, without the full transaction.

### Risks

- **The `Data/Enums` carve-out touches many files' paths.** Mechanical, GUID-safe, but
  do it as its own commit with nothing else in it.
- **The uniques migration** (~100+ assets) is the content risk. Size the migration
  script in the Phase 1 plan before starting; it may gate the phase.
- **`Package` is `[Serializable]` and `StoredPackages` is a serialized field.** A
  transaction working on a copy must write back to the same dictionary instance on
  commit — carried over from the item-movement spec's Phase 1 risk note.
- **`ItemProvider` is a singleton reached from `AbstractItem` constructors today.**
  Breaking that coupling is the point of Seam 1, but every `new EquipmentItem(...)` in
  the debug UI (`InventoryProvider`) is a call site that moves to `ItemGenerator`.
- **Scope creep toward "push packing into the base class."** Tempting during the
  container extraction; it is Tier 4. Extract, break the provider coupling, stop.

### The pattern to copy

`MutableFloat`, `Currency`, `CurrencyDropRoll`, `DragGeometry`, and the in-progress
`ProbabilityTable<T>` are all the same move: **a pure, Unity-free, unit-tested core in
its own assembly, with a thin `ScriptableObject` / `MonoBehaviour` adapter over it.**
The item model and (later) the combat model are the two big clusters that have not had
this treatment. Seam 1 applies it to items. Every deep, tested module in this codebase
already looks like this — the rework makes the item model look like it too.
