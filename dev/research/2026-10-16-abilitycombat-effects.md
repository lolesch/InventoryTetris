# AbilityCombat effects: where they are and what transfers (#297)

Date: 2026-10-16. Issue: lolesch/InventoryTetris#297. Parent map: #293. Blocks: #298.

## Answer

- The `Pawns/Abilities/Effects/` folder named in the spec does not exist in any source I searched. No commit in any branch of `lolesch/ARPG-Combat`, and no commit in the local clones, has a path with `abilit` in it.
- The closest code is `lolesch/ARPG-Combat` at `5400b2b` (main, the only branch). Its effect model is in `Assets/Code/Interfaces/` and `Assets/Code/Pawns/Stats/StatusEffect.cs`, with a 176-line file defining four effect kinds.
- Of the eight kinds in the spec, four exist in code: damage, DoT, stat modifier and heal (over time). Root exists only as an enum value. Shield, immunity and drain are absent. Fill resource exists for health only.
- The effect model itself is plain C# structs and interfaces. Only the receivers (`Pawn`, `Destroyable`) and the `Projectile` that delivers effects depend on scene objects. The spec's "built on scene objects" is true of the receivers, not of the effect model.
- The duration model is a plain `Ticker` advanced by `Time.deltaTime` in `Pawn.Update`. Stat-modifier durations are never ticked, so stat modifiers are permanent.
- Stacking: DoTs and stat modifiers stack without limit and never refresh. The only dedupe is per projectile per receiver.
- Targets are expressed as `IEffectReceiver` components found through the `EnemyCollector` and `DestroyableCollector` static lists, filtered by an XZ radius band. Sources are not expressed at runtime: `IEffectApplier` is never implemented and `StatModifier.origin` is never set.
- Real bug to know before transferring: `Projectile.cs:43-52` applies only the first effect in `SpawnData.effects` to each receiver. The `Circle.asset` example has four effects, so three of them never run through a projectile.

## What was searched

| Where | Result |
|---|---|
| `gh api repos/lolesch/ARPG-Combat/git/trees/HEAD?recursive=1` (1595 paths, main) | No `Abilities` path. Effect files: `Assets/Code/Interfaces/IEffectApplier.cs`, `Assets/Code/Interfaces/IEffectReceiver.cs`, `Assets/Code/Pawns/Stats/StatusEffect.cs`, `Assets/Code/Pawns/Stats/StatModifier.cs`. Skill art and `Items/Skills/` only otherwise. |
| `gh api repos/lolesch/ARPG-Combat/branches` | Only `main`. |
| Local clone `C:/Users/loles/Desktop/LEONID/ARPG Combat`, `git log --all --name-only` | No path matching `abilit`. Effect paths in history include `Assets/Code/Pawn/Stats/Effect.cs` (older, singular `Pawn`, see below) and `Assets/Code/Pawns/Stats/StatusEffect.cs`. Local HEAD equals `origin/main` equals GitHub `main` (`5400b2b`). |
| Local clone `C:/Users/loles/Desktop/LEONID/ARPG`, `git ls-files` and history | No abilities folder. Only `Assets/Code/Pawn/Stats/Effect.cs`, a 26-line struct with a `StatScore` reference and a duration, no logic. The git history contains no abilities path. |
| `gh search code "Pawns/Abilities" --owner lolesch` and `"AbilityCombat" --owner lolesch` | Only hits are `lolesch/InventoryTetris:dev/specs/2026-09-29-deferred-design-work.md` (the spec that names the prior art). |
| `gh repo list lolesch --limit 100` | 14 repos, none named AbilityCombat. `ARPG-Combat` is the only candidate. |

Search limits: `gh search code` covers default branches of indexed repos. It did not search other local folders under `LEONID/` (for example `DungeonCrawler`, `BoneAndBlood`); those are not AbilityCombat candidates by name. Nothing was built or run in Unity.

## Effect kinds

Read as `Assets/Code/Pawns/Stats/StatusEffect.cs` unless noted. Each effect struct is a command: `ApplyEffect(IEffectReceiver)` calls one receiver method (`ReceiveX`), and `RemoveEffect` calls `RemoveX` (`Assets/Code/Interfaces/IEffectReceiver.cs:3-13`). `StatusEffect.cs:165-170` declares the `IEffect` interface.

| Spec kind | In ARPG-Combat | Where | Status |
|---|---|---|---|
| damage | `ResourceEffect`, `ResourceEffectName.Damage` | `StatusEffect.cs:47-68`; `Pawn.cs:109-123` (health minus amount); `Destroyable.cs:13-22` (any damage kills) | Implemented |
| DoT | `ResourceOverTimeEffect`, `ResourceEffectName.Damage` | `StatusEffect.cs:71-137`; `Pawn.cs:31-69` | Implemented (Pawn only; Destroyable kills on first receive) |
| heal, heal over time | same structs, `ResourceEffectName.Heal` | `Pawn.cs:59-60`, `113-115` | Implemented, health only |
| stat modifier | `StatusEffect` (`StatName` + `StatModifier`) | `StatusEffect.cs:9-43`; `Pawn.cs:129-136` | Applied, never removed (see below) |
| root | `CrowdControl.Root = 8` only; `ConditionEffect` with `CrowdControl cc` | `Assets/Code/Enums/CrowdControl.cs`; `StatusEffect.cs:140-163`; `Pawn.cs:142-146` throws `NotImplementedException` | Not implemented: the enum and the data exist, no receiver code |
| shield | none | none | Absent |
| immunity | none; `CrowdControl.Stasis` (`=1`) is a tooltip only | `CrowdControl.cs` | Absent |
| drain | none; damage and heal can be authored in one skill but nothing links them | none | Absent |
| fill resource | `ResourceEffectName.Heal` writes health only; `ResourceName.ManaCurrent` exists but no receiver writes mana | `Assets/Code/Enums/ResourceName.cs`; `Pawn.cs:113-115` | Partial (health only) |

Authored examples: `Assets/Code/Items/Skills/SpawnBehaviour/Circle.asset:39-68` lists StatusEffect (`statName: 5` = AttackDamage, amount -20, duration 5), ResourceEffect (`resourceName: 2` = Damage, amount 30), ResourceOverTimeEffect (`resourceName: 2` = Damage, amount 20, duration 5, repeatRate 0.2) and ConditionEffect (`cc: 8` = Root, duration 2). `Ring.asset` and `MultiProjectile.asset` author DoT and damage only.

Note the enum values: `ResourceEffectName` is `Heal = 1`, `Damage = 2` (`ResourceName.cs`). `resourceName: 2` in the assets is Damage, not mana.

## Duration and tick model

- Clock: `Pawn.Update` (`Pawn.cs:31-72`) with `Time.deltaTime`. This is wall-clock frame time, not a simulation clock.
- `Ticker` (`Assets/Code/Tools/Ticker.cs`, namespace `ARPG.Tools`): a class with `progress`. `HasRemainingDuration` is `progress < duration`. `Tick(dt)` adds to progress. `Restart()` subtracts `duration`, so overshoot is kept. A delayed ticker starts with `progress = duration`, so it is immediately ready.
- ResourceOverTimeEffect (`StatusEffect.cs:129-134`): the main ticker starts at 0 and expires at `duration`. The repeat ticker is delayed, so the first tick fires on the first `Update` after receipt. Then it fires every `repeatRate` seconds.
- Per-tick amount: `amount / Mathf.RoundToInt(duration / repeatRate)` (`Pawn.cs:60`, `63`). Number of ticks is about `round(duration / repeatRate)`; Circle gives 25 ticks of 0.8 for amount 20. If the rounded count is 0 (duration under half the repeat rate), the per-tick amount becomes infinite.
- ResourceEffect: instant, no state.
- StatusEffect: the constructor creates a `Ticker` (`StatusEffect.cs:14-20`), but nothing ticks it, and `Pawn.cs:135` has `// TODO: remove statMod after duration`. Stat modifiers therefore last forever, and `Duration` is ignored.
- ConditionEffect: has a `Ticker` field, but no receiver handles it.

## Stacking and refresh

- DoT (`ResourceOverTimeEffect`): every receive adds a new instance to `activeResourceEffects` (`Pawn.cs:126`). Instances stack additively with no cap. There is no refresh; a second application runs alongside the first. Removal is by struct equality (`Pawn.cs:127`), which compares the `Ticker` references, so it removes the exact instance.
- Stat modifier (`StatusEffect`): `ReceiveStatusEffect` appends a `StatModifier` to `StatScore` (`Pawn.cs:131-133`, `StatScore.cs:35-40`). No dedupe, no refresh, no cap. `RemoveStatusEffect` throws (`Pawn.cs:137-140`), so modifiers cannot be removed. `RemoveAllModifiersOfOrigin` (`StatScore.cs:64-79`) is never called and its loop starts at `statModifiers.Count` (`StatScore.cs:68`), which indexes past the end.
- Dedupe: `Projectile` keeps `alreadyReceivedEffect` (`Projectile.cs:18`), so one projectile applies to each receiver once. Two projectiles apply twice.
- Modifier order: `StatModifierType` is `Flat = 100`, `PercentAdd = 200`, `PercentMult = 300` (`Assets/Code/Enums/StatModifierType.cs`). `StatScore.RecalculateValues` sums Flat, then applies `1 + sum(PercentAdd)`, then multiplies by each PercentMult, then rounds to 4 decimals (`StatScore.cs:81-~110`). The `statModifiers.OrderBy(...).ToList();` line (around `StatScore.cs:97`) discards its result and does nothing.

## Targets and sources

Targets:
- The spawner sets `SpawnData.targetTypes` (`InteractionType.Enemy` or `Destroyable`) and `SpawnData.effects` (`[SerializeReference] List<IEffect>`, `Assets/Code/Items/Scripts/SpawnData.cs:93-96`).
- `Projectile.DetectTargetsInRange` (`Projectile.cs:110-~125`) fills candidates from `EnemyCollector.collection` or `DestroyableCollector.collection` (`Projectile.cs:129-147`), keeps those whose XZ distance is between `InnerRadius` and `OuterRadius`, then calls `TryGetComponent(out IEffectReceiver)`.
- Targets are therefore components that implement `IEffectReceiver` (`Pawn`, `Destroyable`). There is no target ID or query object.
- Minor risk: `TryGetComponent` failure adds a null to `effectReceiver` (`Projectile.cs:119-121`), which would throw later. The collectors only hold Enemy and Destroyable objects, which both implement the interface, so the risk is low.

Sources:
- `IEffectApplier` (`Assets/Code/Interfaces/IEffectApplier.cs`) is the intended source type. Nothing implements it. The only reference is a commented-out line (`Projectile.cs:159-162`).
- `StatModifier.origin` (`Assets/Code/Pawns/Stats/StatModifier.cs:12,26`) is an interface-typed field, so it is not serialized, and no code calls `new StatModifier(`. In practice every modifier has `origin == null`.
- ResourceEffect and the DoT carry no source at all. Damage is not attributed to a caster.

Consequence for the spec's "who its source is" question: ARPG-Combat has no runtime notion of a source. It would need one designed in.

## Scene-object dependence

Depends on scene objects (MonoBehaviour, GameObject, collectors, Unity time):
- `Pawn` (`Pawn.cs:11`): derives from `Interactable` (a MonoBehaviour); `Update` loop; `Kill()` abstract; `Time.deltaTime`.
- `Destroyable` (`Pawns/Enemies/Destroyable.cs:11`): MonoBehaviour; any damage kills.
- `EnemyBase : Pawn` (`Pawns/Enemies/EnemyBase.cs:9`), `PlayerController : Pawn`.
- `Projectile` (`Items/Scripts/Projectile.cs:14`): MonoBehaviour; `transform.position`; `Destroy(gameObject)`; `TryGetComponent`; the collectors.
- The collectors `EnemyCollector` and `DestroyableCollector`: static lists of scene MonoBehaviours.

Plain logic (no scene objects; unit-testable without Unity objects beyond `Mathf`):
- Effect structs: `ResourceEffect`, `ResourceOverTimeEffect`, `StatusEffect`, `ConditionEffect` (`StatusEffect.cs`).
- Interfaces: `IEffect`, `IEffectApplier`, `IEffectReceiver` (`Interfaces/`).
- `Ticker` (`Tools/Ticker.cs`).
- `StatScore`, `StatModifier`, and the enums (`Enums/`). `StatScore` only uses `EditorDebug` for logging.
- `SpawnData` is a ScriptableObject, but its content is data only; effects are authored in `.asset` YAML.

## What transfers

Transferable as an idea, not a port:
- The double-dispatch shape: an effect struct calls `receiver.ReceiveX`, and the receiver interface has one method per kind. This is the only true "command, effect, receiver per kind" structure in the code.
- `Ticker` as a plain accumulator with a delayed start and an overshoot-preserving restart.
- `StatModifier` ordering (Flat, then PercentAdd, then PercentMult), which is a clean and tested-by-shape calculation.
- Radius-band target selection by target type, which is geometry in `Projectile`, not an effect rule.

Not transferable as written:
- The per-frame tick loop in `Pawn.Update` and the `Time.deltaTime` clock. The spec asks for a sim clock.
- `Projectile` as the delivery point, and `alreadyReceivedEffect` as the dedupe rule.
- The `Destroyable` "damage kills" rule.
- The `RoundToInt` tick division for DoTs, which is lossy and divides by zero for short durations.
- Stat-modifier lifetime (none), removal (throws), and source (none).

## Bugs to know before transferring

1. `Projectile.cs:43-52`: the inner `if` sits inside the effects loop, so after the first effect the receiver is in `alreadyReceivedEffect` and the rest are skipped. Only `effects[0]` applies. `Circle.asset` therefore only applies its StatusEffect when used through a projectile.
2. `Pawn.cs:135`: stat modifiers never expire.
3. `StatScore.cs:68`: `RemoveAllModifiersOfOrigin` starts at `Count`, so the first iteration throws `ArgumentOutOfRangeException`. Currently unused.
4. `Destroyable.cs:39-43` and `Pawn.cs:137-146`: `RemoveStatusEffect`, `ReceiveConditionEffect` and `RemoveConditionEffect` throw. `Destroyable.ReceiveStatusEffect` throws too, so a projectile whose first effect is a StatusEffect throws when it hits a Destroyable.
5. `Pawn.cs:60-63`: DoT per-tick amount divides by a rounded count that can be 0.
6. `ResourceOverTimeEffect.ApplyEffect` (`StatusEffect.cs:129-134`) reassigns its own `ticker` and `repeatTicker` on the boxed copy stored in `SpawnData.effects`. Each receiver gets a fresh `Ticker`, so this is safe but mutates shared asset data at runtime.

## Open items

- The spec's prior-art path (`Pawns/Abilities/Effects/`) does not exist. The spec should cite `lolesch/ARPG-Combat` `Assets/Code/Interfaces/` and `Assets/Code/Pawns/Stats/StatusEffect.cs` instead, or the owner should say where the other AbilityCombat code lives.
- Shield, immunity and drain have no prior art in ARPG-Combat and need designing from scratch.
- Stacking, refresh, source and stat-modifier expiry are unspecified in code. The spec's "to decide" list still applies.
