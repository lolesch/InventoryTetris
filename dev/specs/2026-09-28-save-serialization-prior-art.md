# Save format and serialization — prior art and a submodule proposal

Date: 2026-09-28
Status: **Research, pre-spec.** Companion to
`2026-09-28-player-session-flow-research.md` (its Phase C). Input for the save-system
`/to-spec`. The owner settled section 5's calls on 2026-09-28, and section 5 overrides
section 4 wherever they differ.

Scanned: `RuadhWarbands`, `Ruadh2`, `DungeonCrawler`, `ARPG`, `AutoBattler` (local), plus
`lolesch/HellClockBuilder` and `lolesch/CodingTest_TF` (GitHub), plus the `Utility`
submodule at `3eed026`.

---

## 1. What the other repos do

### The memento pair — copied four times, never shared

`RuadhWarbands`, `Ruadh2`, `CodingTest_TF` and `DungeonCrawler` each carry the same two
files under a different namespace:

```csharp
[DataContract] public abstract class AbstractMemento { }

public interface ISerializable<T> where T : AbstractMemento
{
    T Serialize();
    void Deserialize(T memento);
}
```

The usage pattern around it is also identical:

- Every persistent class implements `ISerializable<XMemento>` and nests or neighbours a
  `[DataContract] XMemento : AbstractMemento` with `[DataMember]` fields.
- Composition is recursive: `GameState.Serialize()` calls `Party.Serialize()`, which calls
  `PawnData.Serialize()` per member (`RuadhWarbands/Assets/Code/Data/`).
- References to authored content are saved **by name or enum**, not by asset:
  `PawnDataMemento` stores `PlayerClassType`, `ClanType`, and `Upgrades` as trait *names*.
- Restoring is `new X(memento)` → `Deserialize(memento)`, i.e. mutate-in-place after
  construction.
- The serializer is `DataContractJsonSerializer` writing to a `MemoryStream`.
- A provider singleton does the IO. That is `DataProvider` in Ruadh and
  `SerialisationProvider` in CodingTest_TF, both with the same
  `Save(directory, file, MemoryStream)` / `Load(directory, file)` pair:
  `File.WriteAllBytes`, `Application.persistentDataPath`, and a fixed file extension.
- Versioning is a `VersionString` field on the root memento, with the comment
  `/// patch-fix here` and nothing behind it (`GameState.Deserialize`).
- Settings are a separate memento and a separate file (`SettingsProvider`, Ruadh), saved
  on every setter.

What worked: small, explicit, readable JSON; content referenced by stable key; settings
split from game state. Every project reached for the same shape, which is the case for
putting it in the submodule.

What did not carry over well:

| Gap | Where | Consequence |
|---|---|---|
| Non-atomic write | `File.WriteAllBytes` straight onto the save | a crash mid-write corrupts the only copy |
| No backup, no corrupt-file handling | `// try catch...` left as a comment | a bad file throws on load |
| Version field with no migration | `VersionString`, `patch-fix here` | the first schema change breaks old saves |
| IO behind a MonoBehaviour singleton | `DataProvider.Instance.Save` | untestable in EditMode without a scene; `DataProvider.Instance.Awake()` called by hand |
| `void Deserialize(T)` mutates in place | every originator | forces mutable, default-constructible domain types |
| `ISerializable<T>` name | every repo | shadows `System.Runtime.Serialization.ISerializable` in any file that imports that namespace, which is exactly the namespace `[DataContract]` needs |
| `DataContractJsonSerializer` | every repo | reflection-heavy; skips constructors and field initializers (`GetUninitializedObject`); an IL2CPP stripping risk |

### `DungeonCrawler` — the best store seam

`CloudSaveProvider.IDataClient` is a key/value store interface with two adapters:
`PlayerPrefClient` (Newtonsoft over `PlayerPrefs`) and `CloudSaveClient` (Unity Gaming
Services). That is the `ISaveStore` seam the foundational-rework spec named, already
proven in one of these projects. It is async because of the cloud adapter.

### `HellClockBuilder` — an importer, not a save system

`PlayerSaveData` is a `JsonUtility` mirror of the *commercial game's* save file (most
fields commented out). `GameState.LoadSaveFile` reads it with `JsonUtility.FromJson`.
Useful only as evidence that `JsonUtility` handles a flat `[Serializable]` struct graph;
there is no write path.

### `ARPG` — historical

Uses `BinaryFormatter` (obsolete and unsafe) plus `JsonUtility.FromJsonOverwrite`. Its
`IDataCollection<T> { PopulateData; LoadFromData }` is the same memento idea in older form.
Nothing to take from it.

### `AutoBattler` — nothing

No save code. Its ADR-0012 is about combat, not persistence.

## 2. What the Utility submodule has

**Nothing for persistence.** The only serialization hits are `ISerializationCallbackReceiver`
on `Hex` and `CellTile`, which are inspector plumbing. The assemblies are `Utility` (core),
`Utility.UI`, the Editor asmdefs, and `Submodules.Utility.Tests` / `.TestSupport`. The
submodule already hosts providers (`AbstractSceneSingleton`, `AbstractProvider`),
`Timer`/`Stopwatch` and `Tween`: shared mechanism, no game vocabulary. That is the same
rule ADR-0011 applied to the UI primitives and #69 applies to input.

## 3. What InventoryTetris already has that constrains the choice

- `ItemInstanceDto` / `AffixDto`: `[Serializable]` classes with public fields, arrays,
  and enums stored **by name**. That is exactly `JsonUtility`'s supported subset, and
  already save-shaped.
- `ItemInstance.FromDto` is a **static factory**. `ItemInstance` is immutable (CONTEXT.md
  **Item Instance**), so the repos' `void Deserialize(T)` shape cannot apply to it.
- `FromDto` stores `definitionId` without checking it against `IItemCatalog`. An unknown
  id surfaces later, wherever the catalog is consulted. The load-tolerance rule
  (roadmap A2) therefore belongs in the restore step, where the catalog is available.
- Newtonsoft is in `packages-lock.json` **only transitively** through
  `com.unity.ai.assistant`, a tooling package. Nothing may rely on it unless it is added
  explicitly.
- `apiCompatibilityLevel: 6` (.NET Standard 2.1): `System.IO`, `File.Replace` and
  `DataContractJsonSerializer` are all available.

## 4. Proposal — `Utility.Persistence` in the submodule

The submodule owns the **mechanism**; the game owns the **Dtos and the mapping**. That
is the same seam #69 draws for input.

### In the submodule (new asmdef `Utility.Persistence`)

| Type | Job | Engine? |
|---|---|---|
| `ISaveStore` | `Write(key, string)`, `TryRead(key, out string)`, `Delete(key)`, `Exists(key)`, `Keys()` | no |
| `FileSaveStore(rootDirectory)` | temp file + `File.Replace` with a `.bak`. The directory is **injected**, so `Application.persistentDataPath` lives in the game's composition root and the store tests against a temp dir | no |
| `InMemorySaveStore` | test double; also useful for a "no-save" dev mode | no |
| `PlayerPrefsSaveStore` | optional, for settings | yes |
| `ISaveSerializer` | `string Serialize<T>(T)`, `T Deserialize<T>(string)` | no |
| `JsonUtilitySaveSerializer` | default adapter | yes |
| `SaveEnvelope<T>` | `{ schemaVersion, savedAtUtc, payload }`, the one wrapper every file gets | no |
| `SaveSlot<T>` | store + serializer + envelope. `Save(T)`, `Load() → LoadResult<T>` with status `Missing / Loaded / RestoredFromBackup / Corrupt / NewerVersion`; runs a registered migration chain for older versions | no |
| `IDtoMapper<TDomain, TDto>` | `ToDto(TDomain)` / `FromDto(TDto)` as a **separate object**, so it can take dependencies such as a catalog, and the domain type stays immutable (§5.2) | no |

Deliberately **not** moved:

- **`AbstractMemento` / `ISerializable<T>` as they are.** The `[DataContract]` base ties the
  contract to one serializer. The name collides with the BCL. And `void Deserialize` rules
  out immutable types. The shared contract is the mapper above, which is cheaper than a base class.
- **Any Dto type.** They name game concepts (hero, container, item).
- **Async.** Local single-player saves are small and synchronous. DungeonCrawler's
  cloud adapter is the case for async; if cloud ever lands, `ISaveStore` gains an async
  sibling. Flagged here because retrofitting async onto the interface is a breaking
  change for every consumer.

### Serializer choice — recommended: `JsonUtility` behind `ISaveSerializer`

| | `JsonUtility` | `DataContractJsonSerializer` (repos' choice) | Newtonsoft |
|---|---|---|---|
| Dependency | none | BCL | explicit package, and it spreads to every submodule consumer |
| Fits existing `ItemInstanceDto` | **as is** | needs `[DataContract]` annotations, or falls back to all-fields | as is |
| Dictionaries / polymorphism / null | no / no / no (a null object round-trips as a default instance) | yes / with `KnownType` / yes | yes / yes / yes |
| Constructors run on load | field initializers yes | **no** | yes |
| IL2CPP | safe | stripping risk | needs a `link.xml` or the AOT package |

The save graph is flat: containers are arrays of `{ x, y, instance, amount }`, the hero is
scalars plus arrays, the Corpse is an id plus instances. None of it needs dictionaries or
polymorphism. `JsonUtility` covers it with zero dependencies, and the adapter seam keeps
Newtonsoft one class away if that ever changes. **Rule for Dtos:** `[Serializable]`
classes, public fields, arrays not dictionaries, enums by name, content referenced by
stable string id. That is what `ItemInstanceDto` already does.

### In the game (per the roadmap's Phase C)

Hero, container, Corpse and Account Dtos. The mappers from the live objects, which
take `IItemCatalog` and a Location catalog. The pre-save normalisation (cancel a staged
sale, return a held Package). The composition root that picks
`Application.persistentDataPath`. The migration steps.

### Order, updated

This slots in as the **first step of Phase C** in the roadmap:

0. `Utility.Persistence` in the submodule — store, serializer, envelope, `SaveSlot`, with
   EditMode tests in `Submodules.Utility.Tests`. Commit to the submodule first, then bump
   the pointer (the two-step flow in `docs/agents/codebase-notes.md`; #79 is what happens
   otherwise).
1. onwards: unchanged (container DTOs, level → stats, Location catalog, hero / Account
   Dtos).

An ADR in the style of ADR-0011 — "the save mechanism lives in the Utility submodule; the
save format does not" — records the seam.

## 5. Owner decisions (2026-09-28)

1. **The name is `Dto` (data transfer object).** Game-side save shapes keep the existing
   `…Dto` suffix, as `ItemInstanceDto` does. *Memento* is not adopted.
2. **Everything game-agnostic goes into the submodule.** That covers the store, the
   serializer, the envelope, `SaveSlot<T>`, the migration chain *and* the shared mapping
   contract (`IDtoMapper<TDomain, TDto>`, renamed from `IMementoMapper` per decision 1).
   The game keeps only the Dto types, the concrete mappers and its composition root.
3. **`JsonUtility` for now.** Newtonsoft becomes an explicit submodule dependency if a
   save shape ever needs it; the `ISaveSerializer` seam is what makes that a
   one-adapter change.
4. **No retrofit ticket.** The other consumers adopt the latest submodule on their own
   schedule, as they already do.
