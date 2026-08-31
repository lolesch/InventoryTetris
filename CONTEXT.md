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
two-hander over a weapon and off-hand. Displaced items are re-homed in a fixed order, or
the whole move is refused.
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
iron -5-> copper -12-> silver -20-> gold.
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
