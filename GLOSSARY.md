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
backpack), the **Stash**, a Town Stop's **Supply**, the **Sold tab**, and the
**Equipment** paperdoll.

**Inventory / Stash / Supply / Sold tab**:
Four distinct *roles*, all currently played by the same type. Only Equipment is its own
type. Say which role you mean — "the Stash" is never a class.
The Stash belongs to the **Hero** for the MVP (one hero, so nothing shares it) and
saves with it. The design intent that loot be stored *and exchanged among heroes* needs a
persistence tier above the Session that does not exist yet — revisit post-MVP (ADR-0014).
_Avoid_: using "inventory" to mean any container

**Supply**:
The stock a **Town Stop** offers. Finite: buying removes the item until a **Restock**. The
Vendor has one; a Healer would have its own, with different stock — so a Supply is never
"the vendor's", it is always some Town Stop's.
_Avoid_: store, shop, shelf, vendor container, merchant

**Restock**:
Refilling a Supply with a fresh roll, discarding whatever was left unsold. The thing that
makes a Supply finite rather than endless.
_Avoid_: refresh, reroll, resupply, replenish

**Sold tab**:
Where what the player sold goes. A sale is immediate: shift-click an item, or drop it on the
Supply or the Sold tab, and it is paid out at once and lands here, never on the shelf. The Sold
tab is a **Supply** - its items are bought back like any shelf, at the Markup, so a mis-click
is recoverable but not free. It holds a limited number of Packages; a sale that needs room
discards the oldest. A **Restock** clears it; closing a panel or changing Town Stop leaves it
alone. One container, shown as a tab beside the Supply by every Town Stop that buys (the
Vendor and the Healer). It belongs to the **World**, not the hero, and is not saved (ADR-0016).
_Avoid_: sell basket, basket, cart, buyback, trade window

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

**Quick Move**:
A shift-click or right-click that moves a Package without a drag, to a destination chosen
by the active **Inventory Context** rather than by where a pointer ends up. Each context
names a hub (the **Inventory**), one sink, and a set of sources, and every row follows the
same three rules: the hub goes to the sink, the **Equipment** goes to the sink in any
context but `Hero`, and a listed source comes back to the hub. A context with no rows
moves nothing — the **Supply** is never a sink, and its own shift-click stays a **Buy** in
every context. `Hero` has one row of its own: while a **Run** has a ground, the **Inventory**
drops its item there, into the **Ground Items List**; in Town there is no ground, so `Hero`
moves nothing. A move between containers always executes — into the target container, or the
hand if that is full. A retrieval from the **Stash** and a **Buy** go through the player's
acquisition entry point instead, so auto-equip applies, and when nothing has room they roll
back with nothing moved.
_Avoid_: auto-move, transfer, quick-transfer; "shift-click" (that is the input, not the move)

**Package Origin**:
Where a Package was lifted from — the container and the cell, together, as one thing. Held
for as long as a move is in flight, so the move can be undone.
_Avoid_: sender, source, from, home

**Return to Origin**:
Sending a Package back to its Package Origin: the exact cell if it is still free, else
anywhere in the **Inventory**, else the Package stays on the cursor. It never destroys a
Package and never touches the **Wallet**. The one primitive a cancelled drag, a closed
panel and a purchase returned off the cursor all go through.
_Avoid_: undo, revert, rollback (a rollback is a Transaction's, not a Package's)

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
bundle handed over on Recall. XP is *not* Loot — it is delivered per kill too, but
straight to the hero rather than as a Drop.
_Avoid_: haul, spoils, bounty, take, rewards; XP (a separate reward, delivered straight to the hero)

**Kill**:
One enemy falling. It is the settle unit for every reward — each kill sheds its Drops and
coin Piles on the spot, and delivers its XP to the hero the same tick. Nothing waits for
the Encounter's clear.
_Avoid_: frag, takedown, defeat

**Drop**:
Loot lying on the ground at a Location — shed by a defeated enemy, or laid out from a
Corpse when the hero returns for it — not yet picked up. Drops accumulate as enemies
fall, never as one bundle at the end; a Drop still on the ground when the Run ends is
gone, on Recall or Death alike. By default a Drop lies in the **Ground Items List** until the
player clicks it; the click goes through the player's acquisition entry point (see **Quick
Move**) — auto-equip into an empty slot, else the **Inventory**; with no room it stays on the
ground. A debug switch on the Combat Panel, `AutoPickup`, hands that back to the hero: on, a
Drop the hero's loot filter admits is picked up the same way as it falls, and only the rest
stay down. A coin Pile the filter admits banks to the Wallet on the spot either way.
_Avoid_: pile (that is coins), ground loot, spill, cache

**Corpse**:
The hero's bag, set aside at the Location where they were downed. Death empties the bag
into the Corpse; recovering it means re-entering that Location and picking the items
back up — through the same acquisition entry point as a **Drop**, so gear auto-equips into
an empty slot. There is only ever one — a second Death destroys any Corpse still unclaimed —
and it belongs to the **Hero**: it saves with it and persists between Sessions until
recovered.
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
The single persistent character — the one that fights — and everything that is its own:
its stats, level and XP, its **Equipment**, **Inventory** and **Stash**, its **Wallet**,
its **Behaviour Profile**, the **Location** it is set to be Sent to, and its **Corpse** if
it has one. That is the whole of what saves, and nothing the hero does not own does.
During a Run the player never controls it directly; they set its Behaviour Profile and
its gear and it fights autonomously. One hero per Session; several can be saved and one
loaded (which replaces the Hero and the **World**, see **Session**), but a screen to
choose among them is deferred.
_Avoid_: character, unit, avatar, champion; "player" for the thing in the Field

**Hero save**:
One file holding one **Hero**, named `Name_id`: the hero's name, for the eye, and a generated id
that never changes, which is the identity (a rename moves the file, never the id), and nothing else.
It holds the hero's display name, the id of the template it was built from (`HeroData`: its icon and
class name), level, the current Health, Resource, Shield and
Experience, its **Behaviour Profile**, its selected **Location** (by id), its **Corpse** if
it has one, and the packages in its **Equipment**, **Inventory** and **Stash** by cell
(the **Wallet** is coins in the Inventory, so it saves with it). It never holds the **World**
(**Supply**, the Sold container, the **Run** and its ground **Drops**, the **Inventory
Context**), authored data (the item catalog, `GameConfig`), or a derived stat: a stat is recomputed
from the template, level and gear on load.
A loaded hero is the default template with the file restored over it, so a file is never a
different shape from a new hero. It is written when a **Run** has settled and on quitting,
never while a Run is in the Field.
_Avoid_: save game, profile, character file; "save" for the **Session** (the Hero saves,
the Session holds none)

**Account**:
The tier above the **Hero**: it owns the list of heroes and which one was chosen last, and
nothing the hero does. It is its own small file beside the **Hero saves**. A Stash or Wallet
shared across heroes would be Account state; none is yet (ADR-0014 leaves it open).
_Avoid_: profile, save file, user

**Behaviour Profile**:
The six sliders the player sets on the **Hero** — how it fights and when it Auto-Recalls.
Held by the Hero and saved with it, so a hero keeps its tuning between Sessions. Live
during a Run: the player adjusts it, the hero never decides it.
_Avoid_: settings, preset, loadout; "config" (that is the authored `GameConfig`)

**Player**:
The person at the keyboard. They choose the Location, tune the six sliders, judge when to
Recall, and sort the bag in Town. Every player action is a decision *about* the hero,
never a move *as* it — that is the whole loop.
_Avoid_: user, you; "hero" for the one making the calls

## Runs

**Session**:
The span of play between app start and quit. It contains many Runs and holds one
**Hero** and its **World** at a time. The Hero is what persists: it saves and resumes
`InTown` on the next launch; the World is never saved. A Run never spans Sessions:
quitting mid-Run banks what the hero already picked up and discards the rest. The
Session outlives both — loading a hero swaps the Hero and the World together and the
Session carries on. What saves is the **Hero**, as a **Hero save**; the **Account** above
it only lists the heroes and remembers the last one chosen, and a Stash or Wallet shared
across heroes would be Account state, which is deferred (ADR-0014) — do not let persistence
code assume the Session is the outermost owner. The Session owns no container, Wallet or XP
itself and saves nothing; say "the Hero" for those.
_Avoid_: playthrough, save file (the Hero saves, in a **Hero save**; the Session holds no
save); "session" for the replaceable per-hero unit (that is the **Hero** and its **World**)

**World**:
What a **Session** holds around the **Hero** while that hero is loaded, and never saves:
each Town Stop's **Supply**, the Sold container, the **Run** with its ground **Drops**,
and the **Inventory Context**. A Supply is rolled fresh by a **Restock**, the Sold
container is stock the Town Stops hold, a Run never spans Sessions, and a context is only
what the player has open — so none of it belongs to the hero, and a new Session starts it
fresh. It is replaced together with the Hero when a hero is loaded, as a swap and never a
clearing field by field, so nothing of the previous hero's trading or trip can leak into
the next. It does not hold authored data (the item catalog, distributions, container
sizes and tuning defaults are an immutable `GameConfig`, the same for every hero), the
scene-scoped UI singletons (the drag cursor, the preview, the scene loader), or the
**Corpse**, which is the Hero's.
_Avoid_: Hero State (retired: it lumped the hero's own things in with these); world map,
overworld (see **Field**); Session (the span of play, which outlives it)

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
is spent and the last enemy is down. XP, Loot Drops and coin Piles all arrive per kill as
it ran — the clear pays nothing out, and a Run driven off mid-Encounter forfeits nothing.
A one-second beat, then the next builds. The first Encounter of a Run opens after one
spawn delay (the Location's `SpawnInterval ± SpawnJitter`), not at the moment of Send. A Location
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

**Relocate**:
The player action that moves a live Run to another Location without ending it — the Run
stays `InField`, the current Encounter stops and a fresh one opens at the new Location,
and everything the Run has earned so far carries on into its one result. Not a Recall:
nothing is settled and the hero never passes through Town. Ground Drops at the Location
left behind are gone, as at the end of a Run.
_Avoid_: switch, travel, hop; "Recall and re-Send" (that is what Relocate replaced)

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
A **Town Stop** that instantly refills the hero's Health and Resource each time it is
entered (the refill is the player's resource globes filling; audio feedback is deferred).
Its **Side Panel** shows its own **Supply** of consumables, bought like the Vendor's, and
has a **Sold tab** of its own.
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
lowest-HP enemy within the hero's **Strike Range** - an enemy still walking in cannot be
struck. Always available; gear only scales it. Enemies Strike too, once the hero is within their
own Strike Range, and each archetype declares its Strike's **damage type**: a Brute's is
physical (mitigated by the hero's Armor), a Skirmisher's magical (by his Magic Resist). Enemies
have a Magic Resist of their own, so the hero's Cast is mitigated like his Strike.
_Avoid_: swing, attack (a Cast attacks too), auto-attack, basic attack

**Ground**:
The flat disk the fight takes place on, owned by the simulation (spatial-combat spec). The
hero stands at its **Origin**; enemies spawn a margin beyond its edge, on a bearing the sim
chooses, and walk in. The arena draws positions the sim owns; it never moves anyone.
_Avoid_: map, arena (that is the view), field, board

**Origin**:
The centre of the **Ground**, where the hero calls home and stands until he walks. In the
arena it is drawn at the selected Location's Hero icon; the icon stays the Location's marker
and does not move, while the hero **figure** that stands on the ground is a separate element.
The arena maps ground distance to canvas distance with one adjustable tilt (ADR-0018).
_Avoid_: spawn point, anchor (that is the Hero icon's rect, the view's side of it), centre

**Strike Range**:
How far from the hero a Strike reaches, in ground units. An enemy Strikes only while the hero
is within its Strike Range, so a melee enemy closes in and a ranged one - the same capability
with a longer range - stands off. The unarmed hero has a short one; gear never rolls range.
_Avoid_: reach, melee range, weapon range

**Cast**:
The hero's magical attack — an instant area, paced by how fast `Resource` regenerates against
the cast cost. Of the enemies within **Cast Range** he aims at the one whose shape would catch
the most enemies (the **densest cluster**; a tie goes to the one nearest him, then the earliest
spawned), then deals flat `MagicalDamage`, mitigated by each one's Magic Resist, to every enemy
inside the shape. Enemies only: the hero is never hit. With no enemy in range, or a shape that
would catch nobody, the Cast does not fire and spends nothing. Its cadence and the
`CastThreshold` latch are unchanged, and the hero keeps walking while it fires. The area half
of the kit.
_Avoid_: spell, nuke, ability, skill

**Cast Range**:
How far from the hero an enemy may stand to be aimed at by the **Cast**, in ground units,
read from the **Cast definition**. It limits who the hero aims at, not what the shape hits: an
enemy beyond it is still caught when it stands inside a shape aimed at one within range.
_Avoid_: spell range, reach, aggro range

**Cast definition**:
What a **Cast** is: its **Cast Range**, targeting pattern (an enum with one member, densest
cluster), shape (disk, sector or rectangle), size (an area multiplier on the shape) and anchor
(the shape starts on the aimed-at enemy, or on the hero pointing at it). A tuning value of the
Encounter now, a skill's data once skills exist. Its cost stays the hero's cast cost.
_Avoid_: spell data, skill, ability definition

**Hit event**:
What the simulation announces for every hit that lands - the hero's Strike, each target of his
Cast, and every enemy Strike: who dealt it, who took it, the **damage type**, the **raw amount**
(before mitigation, the **damage spread** applied) and the **lost amount** (what the target
actually lost after its Armor or Magic Resist, and no more than it had). A hit the target fully
mitigates still lands, with nothing lost. The damage numbers and, later, effects read from it.
_Avoid_: damage event, hit callback, damage tick

**Damage spread**:
A tuning fraction that varies each hit around its base damage: a hit rolls a factor between
`1 - spread` and `1 + spread`, from its own random stream apart from the Encounter's, movement
and loot streams. Zero means every hit is its base damage. A stand-in until weapons carry a
real minimum and maximum.
_Avoid_: variance, crit range, damage roll

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
The bulky enemy archetype — high health, slow hard hits (physical damage type), some Armor and a
little Magic Resist, low XP. Like every enemy it is built from modifiable stats. A Pack of
them is a clump for the **Cast** to catch; it is what a single-target
physical build clears best, and what an area build grinds against. Parametric off the
Location's source level.
_Avoid_: tank, heavy, bruiser, ogre, elite

**Skirmisher**:
The fragile enemy archetype — low health, fast light hits (magical damage type), no Armor but
some Magic Resist, high XP. Like every enemy it is built from modifiable stats. The
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

**Town Stop**:
One clickable point on the Minimap's Town side — the **Stash**, the **Vendor** and the
**Healer** today. Exactly one is open at a time, each showing its own **Side Panel**. A
Town Stop is not a **Location**: Locations are the Field destinations a Run is **Sent** to,
and the hero never walks to a Town Stop — the player clicks it.
_Avoid_: location, node, station, shop, destination

**Hero Panel**:
The right-side panel: the **Equipment** paperdoll on top, the **Inventory** below. The
player's own things, as opposed to the Town Stop's on the left. It is a member of *every*
**Inventory Context** — that is the only place its ubiquity is stated, and no panel ever
references it. It opens and closes on its own toggle, and is never opened or closed by
another panel naming it.
_Avoid_: character panel, paperdoll panel (Equipment is the paperdoll), bag panel

**Side Panel**:
A left-side panel showing one **Town Stop**'s context — the Stash or the Vendor today. It
knows only the one **Inventory Context** it was authored with, subscribes to that context,
and derives its own visibility from it; it announces nothing and fades nothing. Shares the
left side with the **Combat Panel**, which replaces the Side Panels for the length of a
Run.
_Avoid_: tab, drawer, sidebar; right-side (that is the **Hero Panel**)

**Inventory Context**:
The single-valued enum (`None`, `Hero`, `Stash`, `Vendor`, `Healer`) that answers two
questions at once: which panels are up, and where a **Quick Move** lands. Entry points
*request* a context; every panel *derives* its visibility from it. A context names the
**Hero Panel** plus at most one Town Stop's panel, so the panel set is derived rather than
announced, and a panel that belongs to every context can never be the thing that names one
(ADR-0013). It is held by the **World** (reached through the inventory service,
ADR-0015) and read by the trade flow for **Quick Move**
routing. Its members are not homogeneous and need not be — the Stash is the player's
own storage, the Vendor is someone else's — because the only question the enum answers is
which context is active. Run phase is not a member: only `None` and `Hero` are reachable
in the field, so a Run *constrains* contexts rather than being one.
_Avoid_: Side Panel Context (retired), trade target, active panel, current context

**Combat Panel**:
The left-side panel, visible only during `InField`. Holds behaviour sliders, enemy
health bars and encounter stats. Fades in on Send, out on Recall/Death. Exclusive with
the **Side Panels** by Run phase, not by a toggle group.
_Avoid_: debug panel, sim panel, fight panel

**Ability Hotbar**:
A centre-bottom bar of ability icons (Strike, Cast) that flash on use and show
cooldown overlays. Minimal v1 is flash-only; cooldown visuals are a follow-up.
_Avoid_: skill bar, action bar, power bar

**Ground Items List**:
A pooled list of slot displays for items lying on the ground. Each entry shows the
item name and icon, supports hover preview and click-to-pick-up. One slot per item,
not spatial. A click picks the **Drop** up through the player's acquisition entry point,
so a full bag leaves it lying there. It also shows what the player dropped there — by Quick
Move or by releasing a drag on the floor slot — and like any **Drop** it is gone when the Run
ends. With no Run there is no ground, and an item released on the floor slot goes back where it
came from.
_Avoid_: loot beam, drop list, world items


## UI Components

The Submodule provides basic components to reuse or derive from.

**Interactive Element**:
It reacts to pointer handler to provide visual feedback. Base class for buttons and toggles.

**Panels**:
A panel is a parent component that groups content. It provides appearance options such as fading in and out, scaling and movement. A panel should always stay enabled, only its alpha is set to 0.

**ExclusiveGroups**:
A collection of mutually exclusive Toggles or Panels of which at most one is active at a time. 
"Activate" deactivates whichever sibling held the slot. 

**Displays and Views**:
A display is the visual representation of a data object. *IDisplay* provides a *Refresh()* call to update the display on data change. This differs from views, that show static data.