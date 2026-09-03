# InventoryTetris

An ARPG inventory-management prototype: a grid backpack with Tetris-shaped items, an
equipment paperdoll, a coin economy, and a loot roll. This glossary pins the words the
project uses, so specs, tickets and code name the same things.

## Items

**Item Definition**:
The immutable template for a kind of item — what a Rare Chest *can be*, before anything
is rolled.
_Avoid_: item type, base item, `ItemTypeData`, `AbstractItemObject`

**Item Instance**:
One rolled item — this chest, with these affixes, at this rarity. Immutable after the
roll; crafting or socketing yields a new instance rather than mutating one.
_Avoid_: item object, `AbstractItem`

**Roll**:
The act of turning a definition plus a roll context into an instance. A definition is
never "generated"; an instance is never "defined".
_Avoid_: generate, spawn, create

**Roll Context**:
The inputs a roll depends on beyond the definition — source level, magic find, and the
loot table in play.

**Package**:
What a container actually stores: one item instance plus an amount, sitting at a grid
position. The unit of movement, not the item itself.
_Avoid_: stack, slot contents, entry

**Affix**:
A rolled stat modifier on an instance. **Implicit stats** are the definition's
guaranteed modifiers and are not rolled; both end up in the same combined list.
_Avoid_: modifier, property, enchantment

**Footprint**:
The grid shape an item occupies. The reason the backpack is a packing problem.
_Avoid_: size, dimensions, area

**Rarity**:
The quality tier — Common, Magic, Rare, Unique. A **Unique** is an ordinary definition
flagged unique with a fixed affix list, not a separate kind of thing.

## Containers

**Container**:
Any grid that holds packages. The four in play are the **Inventory** (the player's
backpack), the **Stash**, the **Store**, and the **Equipment** paperdoll.

**Inventory / Stash / Store**:
Three distinct *roles*, all currently played by the same type. Only Equipment is its own
type. Say which role you mean — "the Stash" is never a class.
_Avoid_: using "inventory" to mean any container

**Store**:
The vendor's shelf. Finite: buying removes the item until a restock.
_Avoid_: shop, vendor container, merchant

**Displacement**:
What happens when a placement pushes stored items out of the way — e.g. equipping a
two-hander over a weapon and off-hand. The item directly under the drop point is the
swap partner: a drag sends it to the hand, a right-click swaps it back into the origin
container and only overflows to the hand if that is full. Any *other* displaced item —
a two-hander's collateral off-hand — must go back to a container; if it will not fit, the
whole move rolls back. At most one item ever lands in the hand, and at most one homeless
item can veto a move. A right-click unequip and a shift quick-move follow the same
overflow rule: into the target container, or the hand if it is full — they always execute.
_Avoid_: swap (a swap is one specific displacement), eviction

**Transaction**:
A move that either completes wholly or leaves every container untouched. The guarantee
that no operation can lose an item mid-move.
_Avoid_: operation, batch, atomic move

## Currency

**Base Unit**:
The value scale everything prices in. One iron is one base unit; gold is 1200.
_Avoid_: gold value, copper value

**Denomination**:
One rung of the coin ladder — iron, copper, silver, gold, cheapest first. The ladder is
iron -5-> copper -12-> silver -20-> gold. Each rung carries a fixed Rarity — iron
Common, copper Magic, silver Rare, gold Unique — so a loot filter reads coins and items
on one scale.
_Avoid_: coin type, tier, currency (currency is the whole system)

**Pile**:
Several coins of one denomination landing as a single drop. The thing that makes a tier
feel *earned* rather than trickled.
_Avoid_: stack (that is a container concern), bundle

**Consolidation**:
Converting coins upward as far as they go, keeping total value identical and leaving the
remainder. Deliberate and manual — coins never upgrade themselves.
_Avoid_: upgrade, merge, convert, exchange

**Wallet**:
The player's spendable money, wherever the coins physically sit. Currently not a module
— the behaviour lives on the container.

## Loot

**Loot**:
The items and coins a kill or a cleared Encounter yields. It drops live during a Run,
not as a bundle handed over on Recall.
_Avoid_: haul, spoils, bounty, take, rewards

**Drop**:
Loot lying on the ground at a Location — shed by a defeated enemy, or laid out from a
Corpse when the hero returns for it — not yet picked up. Drops accumulate as enemies
fall, never as one bundle at the end; a Drop still on the ground when the Run ends is
gone, on Recall or Death alike.
_Avoid_: pile (that is coins), ground loot, spill, cache

**Corpse**:
The hero's bag, set aside at the Location where they were downed. Death empties the bag
into the Corpse; recovering it means re-entering that Location and picking the items
back up. There is only ever one — a second Death destroys any Corpse still unclaimed —
and it persists between Sessions until recovered.
_Avoid_: grave, body, loot bag; remains (reserved for a possible future enemy corpse)

**Distribution**:
An authored, weighted set of outcomes — which category drops, which rarity, which coin.
_Avoid_: table (reserve that for the loot table), chances

**Magic Find**:
The stat that biases a rarity roll toward rarer outcomes. It never changes the odds of
dropping nothing at all.
_Avoid_: item rarity bonus, luck, drop rate

**Cascade**:
The rarest-first walk magic find applies to a rarity roll: try Unique, then Rare, then
Magic, remainder is Common. Distinct from scaling weights, which is what it is not.

**Fail Bucket**:
The share of a roll that yields nothing. Held out of the cascade so magic find cannot
change how often you get a drop, only how good it is.
_Avoid_: no-drop chance, miss, empty

## Runs

**Session**:
The span of play between app start and quit. It contains many Runs and is the unit that
persists — the hero, the four containers, the wallet and XP save per Session and resume
`InTown` on the next launch. A Run never spans Sessions: quitting mid-Run banks what the
hero already picked up and discards the rest.
_Avoid_: playthrough, save file (the save is the Session's shadow, not the thing itself)

**Run**:
One trip from Town to a Location and back — the unit the loop turns on. It ends in a
Recall or the hero's Death. Its two states are named `InTown` and `InField`.
_Avoid_: expedition, sortie, mission, session (a Session is the whole playtime, not one
trip); "run" here is the loot trip, not a test run

**Field**:
Everything on the Location side of a Run, as opposed to Town — "the hero is in the
field" means a Run is underway.
_Avoid_: the wild, outside, overworld, world map

**Town**:
The safe hub every Run starts and ends in, and where inventory management has its full
tools. A Run *state*, never a Location.
_Avoid_: base, camp, hub, hideout; town as a map destination

**Location**:
One authored field destination the hero can be Sent to. It carries the source level
and the loot table a Run there rolls against. Its source level is fixed, never scaled
to the hero: Locations form a difficulty ladder a geared hero outgrows.
_Avoid_: level, zone, area, stage, node, dungeon, map

**Encounter**:
One fight at a Location. Its enemies arrive over the fight — singly, or in Packs that
spike past the player's Engagement count — and it clears when they are all down; the
next begins after a short beat. Loot drops and XP is granted per kill, not banked on
the clear. Whether an Encounter draws from a fixed roster or spawns without end is a
`/prototype` question (ADR-0010).
_Avoid_: battle, fight, combat, wave (that is a Pack)

**Send**:
The player action that starts a Run — choose a Location, commit the hero. Transitions
`InTown → InField`.
_Avoid_: deploy, dispatch, embark, launch

**Recall**:
The player action that ends a Run with everything earned so far kept. Transitions
`InField → InTown`.
_Avoid_: retreat, extract, return, flee, escape

**Death**:
The other way a Run ends — the hero is downed in the Field and returns to Town under a
penalty: lost XP, a fee off banked currency, and the bag set aside as a Corpse.
Equipped gear is never touched; not a game-over.
_Avoid_: defeat, loss, game over, fail, wipe

## Combat

What happens inside an Encounter. Settled by the grilling of 2026-09-02; the numbers and
the finite-vs-endless question are still a `/prototype` (ADR-0010).

**Strike**:
The hero's physical attack — one weapon hit on a `1 / AttackSpeed` cadence against the
single lowest-HP enemy in the fight. Always available; gear only scales it.
_Avoid_: swing, attack (a Cast attacks too), auto-attack, basic attack

**Cast**:
The hero's magical attack — flat `MagicalDamage` to each of the three highest-HP enemies
at once, paced by how fast `Resource` regenerates against the cast cost. The area half
of the kit.
_Avoid_: spell, nuke, ability, skill

**Engagement**:
The player-set count of enemies an Encounter tries to keep on the hero at once. A soft
target the fight refills toward as enemies fall — not a ceiling, because a Pack
overshoots it. The kite-vs-dive knob.
_Avoid_: aggression, aggro, cap, wave size

**Pack**:
Several enemies that enter an Encounter together, overshooting Engagement — a Location's
way of forcing a spike of incoming hits the player cannot tune away. A single arrival,
not a timed round.
_Avoid_: wave, swarm, group (that is the Encounter's whole cast), ambush

**Cast Threshold**:
The `Resource` fraction the hero charges up to before it will start a run of Casts,
after which it spends down to empty and recharges. Low is a continuous trickle of Casts;
high is long silences broken by a burst.
_Avoid_: resource reserve, mana gate, burst threshold
