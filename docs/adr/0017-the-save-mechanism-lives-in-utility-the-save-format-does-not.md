---
status: accepted
---

# The save mechanism lives in the Utility submodule; the save format does not

A hero must survive a restart (`dev/specs/2026-10-05-hero-persistence-design.md`). Persisting
one splits cleanly in two. The first half is how bytes reach disk safely: a key/value store, a
serializer behind a seam, a versioned envelope, a slot that reports Missing, Loaded,
RestoredFromBackup, Corrupt or NewerVersion, a migration chain, and the contract that maps a
domain object to a Dto. None of it names a hero, a container or an item. The second half is
what a save of *this* game contains: the Dtos, the mappers that build them from a Hero, and the
restore that puts one back. All of it names game concepts.

Prior art (`dev/specs/2026-09-28-save-serialization-prior-art.md`) shows the first half copied
four times across sibling projects, each time with the same gaps: a non-atomic write, a version
field with no migration, IO behind a `MonoBehaviour` singleton, and a contract name that shadows
the BCL's `ISerializable`. It is the same case ADR-0011 made for the UI primitives and
`ServiceLocator` made for services.

## Decision

1. **The mechanism goes in the `Utility` submodule, as a new assembly `Utility.Persistence`:**
   the store interface with a file store and an in-memory store, the serializer interface with a
   `JsonUtility` adapter, the envelope, the save slot, the migration chain and
   `IDtoMapper<TDomain, TDto>`. It carries no game vocabulary and takes its save directory by
   injection, so the platform persistent data path is picked only in the game's composition root
   and the file store tests against a temp directory.
2. **The format stays in the game, in `InventorySystem.Persistence`:** the Dtos (hero,
   container, Corpse, Account), the mappers and the restore. The `HeroSaveService` that uses them
   sits in `InventorySystem.Services`.
3. **The shared contract is a mapper object, not a base class.** `IDtoMapper` is a separate
   object, so it can take a catalog, and the domain type stays immutable. The siblings'
   `AbstractMemento` and `void Deserialize(T)` are not adopted: a `[DataContract]` base binds the
   contract to one serializer, and in-place deserialization rules out immutable types such as
   `ItemInstance`.
4. **`JsonUtility` behind the serializer seam.** The save graph is flat (arrays of plain
   `[Serializable]` classes, enums by name, content by stable string id), which `JsonUtility`
   covers with no dependency. Newtonsoft becomes an explicit submodule dependency only if a save
   shape ever needs it, and the seam makes that one adapter.
5. **Synchronous.** Local saves are small. An async sibling interface is added if cloud saves
   land; it is not retrofitted onto this one.

## Considered options

**Copy the memento pair a fifth time.** What every sibling did. It repeats four known gaps and
leaves five copies to keep in step. Rejected.

**Put the whole save system in InventoryTetris.** Simplest today, but the store, envelope and
migration chain have no game vocabulary, and AutoBattler would copy them. Rejected for the
reason ADR-0011 gave.

**Put the Dtos in the submodule too.** They name heroes, containers and items, so the submodule
would need the game's vocabulary, which it must not know. Rejected.

## Consequences

- The submodule gains an assembly additively; AutoBattler and the other consumers are unaffected
  and adopt it on their own schedule. No retrofit ticket.
- A change lands in two steps: commit to the submodule first, then bump the pointer
  (`docs/agents/codebase-notes.md`). Bumping first is how #79 happened.
- The Stash and Wallet sections of the hero Dto stay separable from the rest of the hero, so
  lifting them to a tier above the Session later changes their owner and not the format
  (ADR-0014).
- The save directory is chosen in one place, the composition root, so tests use an in-memory
  store and never touch the real folder.
- A format change is a schema version bump plus a migration step written in the game, never a
  silent field change.
