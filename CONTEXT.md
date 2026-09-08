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
The items and coins a kill sheds. It drops live during a Run, **per kill**, not as a
bundle handed over on Recall. XP is *not* Loot — it settles per Encounter clear (see
**Encounter**), on its own rhythm.
_Avoid_: haul, spoils, bounty, take, rewards; XP (a separate reward, separately timed)

**Kill**:
One enemy falling. It is the settle unit for **Loot** — each kill sheds its Drops and
coin Piles on the spot — and nothing else: XP settles per Encounter clear, not per kill.
"Per kill" and "on the clear" are the two reward rhythms; name which one you mean.
_Avoid_: frag, takedown, defeat; "kill" as the XP unit

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

## The hero

**Hero**:
The single persistent character a Session owns — the one that fights. During a Run the
player never controls it directly; they set its behaviour sliders and its gear and it
fights autonomously. One hero per Session for the MVP; picking from among several saved
heroes is deferred.
_Avoid_: character, unit, avatar, champion; "player" for the thing in the Field

**Player**:
The person at the keyboard. They choose the Location, tune the six sliders, judge when to
Recall, and sort the bag in Town. Every player action is a decision *about* the hero,
never a move *as* it — that is the whole loop.
_Avoid_: user, you; "hero" for the one making the calls

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
One authored field destination the hero can be Sent to. It carries the source level, the
loot table a Run there rolls against, which enemy **archetype** it **Packs**, and its
**Roster** and **Spawn Profile**. Its source level is fixed, never scaled to the hero:
Locations form a difficulty ladder a geared hero outgrows. Two for the MVP — Thornwood
(low source level, Brute-packed, a bag run) and Ashfall (high, Skirmisher-packed, a
gear-gated wall).
_Avoid_: level, zone, area, stage, node, dungeon, map

**Encounter**:
One build-and-release of pressure at a Location — the pacing unit a Run is made of. It
fields a fixed **Roster**; enemies arrive over it per the **Spawn Profile** — one
archetype in **Packs**, the other singly — pressure mounts, and it clears when the Roster
is spent and the last enemy is down. **XP settles here, on the clear**, summed over the
Roster; a Run driven off mid-Encounter forfeits that Encounter's XP. Loot Drops and
coin Piles fell per kill as it ran. A one-second beat, then the next builds. A Location
runs Encounters endlessly at a fixed difficulty; only Recall or Death ends the Run
(issue-#18 `/prototype` pass 2, ADR-0010).
_Avoid_: battle, fight, combat, room; wave (a Pack is one arrival *within* an Encounter)

**Roster**:
The enemies one Encounter will field — authored on the Location as a `[min,max]` count
for **each** archetype (so many Brutes, so many Skirmishers), rolled fresh per Encounter.
The Encounter clears once the whole Roster is spent and dead. Just the counts — the
arrival order is the **Spawn Profile**.
_Avoid_: wave, party, squad, spawn list, the Encounter's cast

**Spawn Profile**:
How a Roster arrives: which archetype comes in **Packs** and which trickles in one at a
time, the Pack size, how often a spawn fires, and the odds the next spawn is the packed
type. The knob that makes two Locations of the same source level feel different.
_Avoid_: spawn table, wave schedule, spawner, timeline

**Send**:
The player action that starts a Run — choose a Location, commit the hero. Transitions
`InTown → InField`.
_Avoid_: deploy, dispatch, embark, launch

**Recall**:
The player action that ends a Run with everything earned so far kept. Transitions
`InField → InTown`.
_Avoid_: retreat, extract, return, flee, escape

**Auto-Recall**:
A Recall the hero performs on its own, because a behaviour slider the player set before
or during the Run said to — health dropping to the retreat fraction, or the bag filling
to the recall fraction. Same transition and same result as a clicked Recall: everything
kept, no penalty. It is still the player's decision, made in advance; the hero never
decides anything.
_Avoid_: auto-retreat, bail, panic button, auto-flee

**Death**:
The other way a Run ends — the hero is downed in the Field and returns to Town under a
penalty: lost XP, a fee off banked currency, and the bag set aside as a Corpse.
Equipped gear is never touched; not a game-over.
_Avoid_: defeat, loss, game over, fail, wipe

**Healer**:
A Town action that instantly refills the hero's Health and Resource. A one-shot
button today; will gain its own side panel later.
_Avoid_: shrine, fountain, well

## Combat

What happens inside an Encounter. Settled by the grilling of 2026-09-02 and the issue-#18
`/prototype`, which ran twice (ADR-0010): fixed Roster, endless Encounters at fixed
difficulty, **two enemy archetypes** (Brute / Skirmisher) with a Location Packing one,
XP settling per Encounter clear. The packed archetype decides which build a Location
favours — single-target **physical** counters a Brute pack, area **magical** counters a
Skirmisher swarm, **hybrid** is the safe middle that owns neither. Combat constants have
prototype starting points but are not frozen.

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
not a timed round. Only the packed archetype arrives in Packs; the other trickles in
singly.
_Avoid_: wave, swarm, group (that is the Encounter's whole cast), ambush

**Brute**:
The bulky enemy archetype — high health, slow hard hits, some Armor, low XP. The **Cast**
(highest-HP targeting) tends to land on Brutes; a Pack of them is what a single-target
physical build clears best, and what an area build grinds against. Parametric off the
Location's source level.
_Avoid_: tank, heavy, bruiser, ogre, elite

**Skirmisher**:
The fragile enemy archetype — low health, fast light hits, no Armor, high XP. The
**Strike** (lowest-HP targeting) tends to pick off Skirmishers; a swarm of them is what
an area magical build clears best, and what a single-target build gets overwhelmed by.
Parametric off the Location's source level.
_Avoid_: minion, add, runner, rusher, trash

**Cast Threshold**:
The `Resource` fraction the hero charges up to before it will start a run of Casts,
after which it spends down to empty and recharges. Low is a continuous, evenly-spaced
stream of Casts; high is the same Casts arriving clumped. A rhythm knob, not a power one
— the `/prototype` found it barely moves an Encounter's outcome against a continuous
spawn (ADR-0010).
_Avoid_: resource reserve, mana gate, burst threshold

## Screen Layout

**Minimap**:
The centre-screen element that always shows. Two visual states — Town and Field — each
with its own background art and button set. Drives panel open/close and Run transitions.
_Avoid_: world map, compass, hud map

**Side Panel**:
A right-side panel that shows one of the town's interactable contexts at a time
(Stash or Vendor today). Exactly one active at a time; the active one is tracked as
`SidePanelContext` on the `InventoryProvider`, which the trade flow reads for
shift-click routing.
_Avoid_: tab, drawer, sidebar

**Side Panel Context**:
The enum (`None`, `Stash`, `Vendor`) that records which town side panel is currently
open. Owned by the `InventoryProvider`, not by the UI toggles. The trade flow queries
it to decide where shift-clicked items land.
_Avoid_: trade target, active panel, current context

**Combat Panel**:
The left-side panel, visible only during `InField`. Holds behaviour sliders, enemy
health bars and encounter stats. Fades in on Send, out on Recall/Death.
_Avoid_: debug panel, sim panel, fight panel

**Ability Hotbar**:
A centre-bottom bar of ability icons (Strike, Cast) that flash on use and show
cooldown overlays. Minimal v1 is flash-only; cooldown visuals are a follow-up.
_Avoid_: skill bar, action bar, power bar

**Ground Items List**:
A pooled list of slot displays for items lying on the ground. Each entry shows the
item name and icon, supports hover preview and click-to-pick-up. One slot per item,
not spatial.
_Avoid_: loot beam, drop list, world items
