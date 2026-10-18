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
guaranteed modifiers, never drawn from the affix pool, though an implicit's value may roll within its
base's range and grow with item level; both end up in the same combined list. An affix on a stat its
item carries as an **item stat** changes the item; any other changes the hero.
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
discards the oldest. A sale never refuses for lack of coin room: the **Wallet** banks the payout
highest denomination first, and the coins the bag has no cell for lie on the **Ground**, one stack per
denomination, highest first (a stack the full Ground pushes out banks first). A **Restock** clears it; closing a panel or changing Town Stop leaves it
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
every context. `Hero` has one row of its own: the **Inventory** drops its item on the
**Ground**, into the **Ground Items List**, in Town as in a **Run**. `Ground` has that row and one
more: a shift-click on the ground picks the **Drop** up. A move between containers always executes — into the target container, or the
hand if that is full. A retrieval from the **Stash** and a **Buy** go through the player's
acquisition entry point instead, so auto-equip applies, and when nothing has room they roll
back with nothing moved. A coin that comes back to the **Inventory** this way, or off the
**Ground**, leaves the **Wallet**'s coins gold first, iron last; a coin sent to the **Stash** just
takes the first free cell.
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
— the behaviour lives on the container. Coins that arrive together (a sale payout, a Pile,
**Consolidation**) are laid gold, silver, copper, iron into the first free cells in that
order; a container short of cells keeps the highest denominations and the Wallet hands back the rest.
A single coin the player moves into the bag, by drop or **Quick Move**, re-lays all the bag's coins that
way, folding none; into the **Stash** or the **Ground** it takes the first free cell. So the bag's coins
are in order, and shift-clicking them to the Stash one by one fills the Stash in order.

## Loot

**Loot**:
The items and coins a kill sheds. It drops live during a Run, **per kill**, not as a
bundle handed over when the Run ends. XP is *not* Loot — it is delivered per kill too, but
straight to the hero rather than as a Drop.
_Avoid_: haul, spoils, bounty, take, rewards; XP (a separate reward, delivered straight to the hero)

**Kill**:
One enemy falling. It is the settle unit for every reward — each kill sheds its Drops and
coin Piles on the spot, and delivers its XP to the hero the same tick. Nothing waits for
the Encounter's clear.
_Avoid_: frag, takedown, defeat

**Drop**:
Loot lying on the ground at a Location — shed by a defeated enemy — not yet picked up.
A recovered **Corpse** is never laid out as Drops. Drops accumulate as enemies
fall, never as one bundle at the end; a Drop still on the ground when the Run ends is
gone, on a Return or a Death alike. The ground is a stash-sized grid: a Drop lies at the cell it
landed in and never moves, and when a new package does not fit the oldest ones are removed
until it does (one larger than the whole grid is refused). By default a Drop lies in the
**Ground Items List** until the player clicks it; the click goes through the player's acquisition
entry point (see **Quick Move**) — auto-equip into an empty slot, else the **Inventory**; with no
room it stays on the ground. A debug switch on the Combat Panel, `AutoPickup`, hands that back to the hero: on, a
Drop the hero's loot filter admits is picked up the same way as it falls, and only the rest
stay down. A coin Pile follows the same rule: with `AutoPickup` on and the filter admitting its
denomination the Wallet takes what it can, and the rest — or the whole Pile, with `AutoPickup`
off — lies on the ground as a stack of that denomination's coin, merging with the stack already
there up to the item's stack limit and making it the newest; denominations never consolidate on
their own. A coin stack pushed out by newer loot banks to the Wallet first and only what the
Wallet cannot take is lost, and it does not count toward the **Run**'s take; clicking one banks
it, and what the Wallet cannot take stays.
_Avoid_: pile (that is coins), ground loot, spill, cache

**Ground**:
Where **Drops** lie: one stash-sized grid, owned by the **World**, in Town as in a **Run**. A Run's
Drops are wiped when the Run ends. In Town the player lays an item there by Quick Move or by releasing a
drag on the ground slot, picks it up from the **Ground Items List**, and it lies there until picked up
and is wiped on **Send**; the coins a sale could not bank lie there too. The **Ground Items List** and a grid of the same cells on the Combat Panel
are two views of it. What lands and what is evicted is the **Drop**'s rule.
_Avoid_: floor, loot area, arena (that is where the fight takes place)

**Corpse**:
Everything the hero wore and carried — worn gear, bag and coins — set aside at the Location
where they were downed, each package with the cell it lay in. Death leaves the hero bare;
recovering the Corpse means re-entering that Location, and each package goes back where it
lay: worn gear to its slot, a bag item to its bag cell (it never equips itself), coins to
their cells. A package whose place is taken goes wherever it still fits, worn gear to an
empty slot first. What does not fit stays on the Corpse, re-buried at the same Location to come
back for; a recovery never lays anything on the ground. There is only ever one — a second Death destroys any Corpse still unclaimed —
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
its stats, level and XP, its lifetime count of **Kills**, its **Equipment**, **Inventory** and **Stash**, its **Wallet**,
its **Behaviour Profile**, the **Location** it is set to be Sent to, the **Unlockables** it
has **Completed**, and its **Corpse** if it has one. That is the whole of what saves, and
nothing the hero does not own does.
During a Run the player never controls it directly; they set its Behaviour Profile and
its gear and it fights autonomously. One hero per Session; several can be saved and one
loaded (which replaces the Hero and the **World**, see **Session**), chosen on the
**Heroes screen**.
_Avoid_: character, unit, avatar, champion; "player" for the thing in the Field

**Level-up**:
The **Hero** reaching its next level from XP. It adds a small, fixed growth to the hero's base
stats, the same for every hero, and heals nothing: the hero's survival is its gear's job.
_Avoid_: ding; "level" for a **Location**'s source level

**Hero save**:
One file holding one **Hero**, named `Name_id`: the hero's name, for the eye, and a generated id
that never changes, which is the identity (a rename moves the file, never the id), and nothing else.
It holds the hero's display name, the id of the template it was built from (`HeroData`: its icon and
class name), level, the current Health, Resource, Shield and
Experience, its total **Kills** (banked when a **Run** settles), its **Behaviour Profile**, its selected **Location** (by id), whether its **Town Portal** is open,
the **Unlockables** it has **Completed** (by id; every other **Unlock State** is derived from them), its **Corpse** if
it has one, and the packages in its **Equipment**, **Inventory** and **Stash** by cell
(the **Wallet** is coins in the Inventory, so it saves with it). It never holds the **World**
(**Supply**, the Sold container, the **Run** and its ground **Drops**, the **Inventory
Context**), authored data (the item catalog, `GameConfig`), or a derived stat: a stat is recomputed
from the template, level and gear on load.
A loaded hero is the default template with the file restored over it, so a file is never a
different shape from a new hero. It is first written when the player starts the game with a new hero
(a **Draft**), then when a **Run** has settled, on quitting, and the moment a **Location** or **Quest**
**Completes**, even mid-Run (what a quit at that moment would write); otherwise never while a Run is live or held.
_Avoid_: save game, profile, character file; "save" for the **Session** (the Hero saves,
the Session holds none)

**Account**:
The tier above the **Hero**: it owns the list of heroes and which one was chosen last, and
nothing the hero does. It is its own small file beside the **Hero saves**. A Stash or Wallet
shared across heroes would be Account state; none is yet (ADR-0014 leaves it open).
_Avoid_: profile, save file, user

**Behaviour Profile**:
The four sliders the player sets on the **Hero** — how it fights, whom it picks, what it picks up and how fast the fight runs.
Held by the Hero and saved with it, so a hero keeps its tuning between Sessions. Live
during a Run: the player adjusts it, the hero never decides it.
_Avoid_: settings, preset, loadout; "config" (that is the authored `GameConfig`)

**Player**:
The person at the keyboard. They choose the Location, tune the four sliders, judge when to
Recall or Return, and sort the bag in Town. Every player action is a decision *about* the hero,
never a move *as* it — that is the whole loop.
_Avoid_: user, you; "hero" for the one making the calls

## Runs

**Session**:
The span of play between app start and quit. It contains many Runs and holds one
**Hero** and its **World** at a time. The Hero is what persists: it saves and resumes
in Town on the next launch; the World is never saved. A Run never spans Sessions:
quitting mid-Run banks what the hero already picked up and discards the rest, and a Run
held by a **Recall** is quit the same way, leaving only its **Town Portal** open. The
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
The unit the loop turns on: from a **Send** until a **Return** or the hero's
**Death**, across every **Encounter** and **Relocate** in between. It is **live** while its
Encounter runs, or **held** while the hero is in Town through a **Recall**, its Encounter
frozen as it was; with no Run there is neither. The Run's state decides only the Run's own
rules (what Send, Relocate, Recall and Return may do, whether the simulation ticks, when the
hero saves), never what can be opened: that follows the **Town map** or **Field map**.
_Avoid_: expedition, sortie, mission, session (a Session is the whole playtime, not one
trip); "run" here is the loot trip, not a test run

**Field**:
Everything on the Location side, as opposed to Town: where the hero is while the **Field map**
is up. Being in the Field does not mean a Run is live: the hero stands at the **Field Exit**
with no Run on the way out, and with a held one after a **Recall**.
_Avoid_: the wild, outside, overworld, world map

**Town**:
The safe hub, where the hero is while the **Town map** is up and where inventory management
has its full tools. A place, not a Run state: a Run held by a **Recall** is still a Run while
the hero stands in Town. Never a Location.
_Avoid_: base, camp, hub, hideout; town as a map destination

**Location**:
One authored field destination the hero can be Sent to, once it is **Active**. It carries
the source level, the loot table a Run there rolls against, which enemy **archetype** it
**Packs**, its **Roster** and **Spawn Profile**, and its **Population**. An **Unlockable**: it is
**Complete** for a hero once the hero clears its whole Population in one Send. Its source level
is fixed, never scaled to the hero: Locations form a difficulty ladder a geared hero outgrows.
Two for the MVP — Thornwood (low source level, Brute-packed, a bag run) and Ashfall (high,
Skirmisher-packed, a gear-gated step: clearable in gear of its level, survivable in part without it).
_Avoid_: level, zone, area, stage, node, dungeon, map

**Encounter**:
One build-and-release of pressure at a Location — the pacing unit a Run is made of. It
fields a fixed **Roster**; enemies arrive over it per the **Spawn Profile** — one
archetype in **Packs**, the other singly — pressure mounts, and it clears when the Roster
is spent and the last enemy is down. XP, Loot Drops and coin Piles all arrive per kill as
it ran — the clear pays nothing out, and a Run driven off mid-Encounter forfeits nothing.
A one-second beat, then the next builds. The first Encounter of a Run opens after one
spawn delay (the Location's `SpawnInterval ± SpawnJitter`), not at the moment of Send. A Send
fields the Location's **Population** of Encounters and no more; once the last one clears, the
Location is **Complete** and the hero idles where it stands until the player Returns, Recalls or
clicks another Location. Only Return or Death ends the Run.
_Avoid_: battle, fight, combat, room; wave (a Pack is one arrival *within* an Encounter);
"complete" for an Encounter (an Encounter **clears**; a Location **Completes**)

**Population**:
How many **Encounters** one Send to a **Location** fields: authored on the Location as a
range with jitter, rolled fresh on every Send and Relocate, so a Location repopulates each
time the hero goes there. The Location's size, in the sense of a map's size.
_Avoid_: wave count, depth, floor count, map size

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
The player action that starts a Run — choose a Location on the **Field map**, commit the
hero. The Run is live at once; the hero figure **Travels** there afterwards.
_Avoid_: deploy, dispatch, embark, launch

**Recall**:
The player action that takes the hero to Town mid-Run without ending it: it spends a
**Portal Scroll** from the Inventory at once and opens a **Town Portal** beside the hero. The
portal takes a moment to open, during which the Run stays live and the hero keeps fighting;
only a **Death** in that moment cancels it, scroll gone. Once open, the Run is held, its
Encounter frozen, until the portal takes him back. Refused in Town, while the hero is down,
and with no Portal Scroll in the Inventory. A dedicated button and key, as in a MOBA's recall.
_Avoid_: return (that ends the Run), teleport, port, extract, flee, escape

**Return**:
The player action that ends a Run with everything earned so far kept: a click on the
**Field Exit** while the Run is live. The hero walks out, and the Encounter he left is gone.
On a held Run the Field Exit only brings the **Town map** back.
_Avoid_: recall (that holds the Run), Auto-Recall (retired: the hero never leaves a Run on his own),
retreat, extract, flee

**Town Portal**:
The link a **Recall** opens, from where the hero stood to the **Waypoint**, open until it is
used. Used from Town, or from its marker on the **Field map**, it takes the hero back and
resumes the held Run where it was, and closes. A click on any **Location** instead is a
**Relocate** and closes it. Saved with the hero, so a portal left open survives a quit; the
Run it held does not, so a portal used after a relaunch Sends there with a fresh **Population**.
_Avoid_: portal (alone, for the scroll), gate, teleporter, waypoint (that is where it opens)

**Portal Scroll**:
The consumable a **Recall** spends, one per Recall, counted from the Inventory only. Sold by
the Healer, which always stocks some, and dropped as **Loot**; a new hero starts with none but
with the coins to buy a few.
_Avoid_: town scroll, recall scroll, teleport scroll, TP

**Relocate**:
The player action that moves a live Run to another Location without ending it — the Run
stays live, the current Encounter stops and a fresh **Population** is rolled at the new Location,
and everything the Run has earned so far carries on into its one result. Not a Return:
nothing is settled. Ground Drops at the Location left behind are gone, as at the end of a
Run. A Location clicked while the Run is held is a Relocate too, even the held one: the held
Encounter is discarded and the **Town Portal** closes.
_Avoid_: switch, travel, hop; "Return and re-Send" (that is what Relocate replaced)

**Death**:
The other way a Run ends — the hero is downed in the Field and returns to Town under a
penalty: lost XP, a fee off banked currency, and everything the hero wore and carried
(coins included) set aside as a Corpse, leaving him bare; not a game-over. The penalty is settled the moment the hero
goes down; the player sees him fall, then is told what was lost and where the Corpse lies, and
stays in the Field until they **Respawn**.
_Avoid_: defeat, loss, game over, fail, wipe

**Respawn**:
The player acknowledging a **Death**: the hero is revived at full Health and Resource and the
Minimap crossfades back to Town, the **Hero figure** standing at the **Waypoint**. It decides
nothing — the Death was settled when the hero went down — so it is the only way on from the
Death notice, and backing out of the notice is a Respawn too.
_Avoid_: revive, resurrect, continue, retry

**Healer**:
A **Town Stop** that instantly refills the hero's Health and Resource each time it is
entered (the refill is the player's resource globes filling; audio feedback is deferred).
Its **Side Panel** shows its own **Supply** of consumables, bought like the Vendor's, and
has a **Sold tab** of its own.
_Avoid_: shrine, fountain, well

## Progression

**Unlockable**:
Anything a hero opens by progressing: every **Location**, every **Quest**, and a **Town Stop**
only when one is authored as locked (the Vendor; the Stash and the Healer are always open and are
not Unlockables). Each has an **Unlock State** per hero, and a Location or Quest names the
Unlockables it **Unlocks**.
_Avoid_: node, gate, content, feature

**Unlock State**:
Where an **Unlockable** stands for one hero: **Hidden**, **Locked**, **Active**, **Complete**,
in that order, and it only ever moves forward. Hidden is not shown; Locked is shown but cannot
be used, because something that Unlocks it is Active but has not reached the state it needs;
Active can be used (a Location Sent to, a Quest worked on, a Town Stop opened); Complete is
Active and done. "Active" in a rule means Active or Complete. Only Complete is saved; the
other states follow from it.
_Avoid_: unlocked, available, open, enabled, discovered (say Active); finished, done,
cleared (say Complete)

**Unlock**:
The link from one **Unlockable** to another: when the first reaches **Active**, or reaches
**Complete**, the second becomes Active. Authored on the first, the one that does the
unlocking. An Unlockable listed by several becomes Active when any one of them gets there; an
Unlockable no Unlock names is Active from the start.
_Avoid_: requirement, prerequisite, dependency, parent (as a domain word)

**Complete**:
The last **Unlock State**: a **Location** whose whole **Population** the hero cleared in one
Send, or a **Quest** whose objective was met while it was **Active**. Recorded on the hero at
the moment it happens, never inferred later. A **Town Stop** never Completes.
_Avoid_: full clear, cleared (for a Location), finished, done, beaten

**Quest**:
An authored **Unlockable** with one objective: another Unlockable reaching **Complete** while
the Quest is **Active** — "Free the merchant" Completes when Thornwood does. It exists to
Unlock things: Locations, Town Stops and further Quests. Only a Completion that happens while
the Quest is Active counts, so a Quest that asks for something the hero did earlier asks for
it again.
_Avoid_: mission, task, objective (the objective is the Quest's condition), achievement

## Combat

What happens inside an Encounter. Settled by the grilling of 2026-09-02 and the issue-#18
`/prototype`, which ran twice (ADR-0010): fixed Roster, endless Encounters at fixed
difficulty (since replaced by a **Population** per Send), **two enemy archetypes** (Brute / Skirmisher) with a Location Packing one,
XP settling per Encounter clear. The packed archetype decides which build a Location
favours — single-target **physical** counters a Brute pack, area **magical** counters a
Skirmisher swarm, **hybrid** is the safe middle that owns neither. Combat constants have
prototype starting points but are not frozen.

**Strike**:
The hero's physical attack — one weapon hit on a `1 / AttackSpeed` cadence against the
hero's one **Sticky Target**, walked to until it is within his **Strike Range** - an enemy
still walking in cannot be struck. Always available; gear only scales it. Enemies Strike too, once the hero is within their
own Strike Range, and each archetype declares its Strike's **damage type**: a Brute's is
physical (mitigated by the hero's Armor), a Skirmisher's magical (by his Magic Resist). Enemies
have a Magic Resist of their own, so the hero's Cast is mitigated like his Strike.
_Avoid_: swing, attack (a Cast attacks too), auto-attack, basic attack

**Arena**:
The flat disk the fight takes place on, owned by the simulation (spatial-combat spec). The
hero stands at its **Origin**; enemies spawn a margin beyond its edge, on a bearing the sim
chooses, and walk in. The arena view draws positions the sim owns; it never moves anyone.
Distances in it are in arena units.
_Avoid_: map, field, board, terrain, ground (that is where **Drops** lie)

**Origin**:
The centre of the **Arena**, where the hero calls home and stands until he walks. In the
arena view it is drawn at the selected Location's Hero icon; the icon stays the Location's marker
and does not move, while the hero **figure** that stands in the arena is a separate element.
The arena view maps arena distance to canvas distance with one adjustable tilt (ADR-0018).
_Avoid_: spawn point, anchor (that is the Hero icon's rect, the view's side of it), centre

**Brain**:
What decides, tick by tick, what one fighter in an **Encounter** does. The hero's is steered by the
player's **Behaviour Profile**, so the player still never controls him directly; an enemy's is to
close in on the hero and **Strike**. A Brain only asks (see **Intent**); the Encounter decides
whether the ask is allowed. Swapping a fighter's Brain changes who it fights like and nothing else.
_Avoid_: AI (a Brain may be a player), controller, driver, behaviour (that is the **Behaviour Profile**)

**Intent**:
What a **Brain** asks of its fighter in one tick: **Approach** a target, **Strike** a target,
**Cast** (aimed where the Encounter finds the densest cluster, as it fires), or - for the hero -
**Walk** to a point. An Approach names its target, not a point, so it
follows a target that moves; a Walk stops on its point. The Encounter carries an
Intent out only when the rules allow it (in reach, off cooldown, enough Resource); an Intent it
refuses costs nothing.
_Avoid_: command, order, input, action

**Strike Range**:
How far from the hero a Strike reaches, in arena units. An enemy Strikes only while the hero
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
How far from the hero an enemy may stand to be aimed at by the **Cast**, in arena units,
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

**Damage number**:
The figure that rises from whoever a hit lands on - an enemy, or the hero figure - showing the
amount lost. Its size shows how big the hit was: the raw amount against the **reference
maximum**, the dealer's best hit of that damage type (the hero's damage stat with the spread
applied; for hits on the hero, the strongest of that type among the archetypes the Location can
field), from a minimum to a maximum size. Magical numbers are tinted dark blue-purple, physical
keep the gold. One number per target and damage type per frame, so a Strike and a Cast on one
enemy are two.
_Avoid_: floating text, damage popup, hit marker

**Damage spread**:
A tuning fraction that varies each hit around its base damage: a hit rolls a factor between
`1 - spread` and `1 + spread`, from its own random stream apart from the Encounter's, movement
and loot streams. Zero means every hit is its base damage. A stand-in until weapons carry a
real minimum and maximum.
_Avoid_: variance, crit range, damage roll

**Stat**:
A value a combatant or an item carries, built from a base and modifiers. Most are read as one total;
some, like Penetration, are read by their flat and percent parts.
_Avoid_: attribute, property

**Item stat**:
A stat an equipped item carries itself, starting from its implicit - the Armor of an Armor base. Affixes on
it change the item, never the hero, and the item adds its total to the hero's stat of the same name.
Equipment never changes a hero stat that has an item stat.
_Avoid_: local modifier, enhanced defense, local stat

**Armor**:
The defense against physical damage: a rating that mitigates more the higher it climbs and never all of a
hit. On the hero it is the sum of his worn items' Armor.

**Magic Resist**:
The defense against magical damage: a percent of each hit, capped. It stands in for elemental
resistances until they exist, and rolls on the hero from a few slots.
_Avoid_: magic armor, resistance (alone)

**Mitigation**:
The share of a hit the target's Armor or Magic Resist removes. Below 0 either defense makes the hit larger,
at most double.
_Avoid_: damage reduction, block

**Penetration**:
The attacker's stat that makes his hit read less of the target's Armor or Magic Resist, by points and by
share. It ignores a defense, never takes one below 0, and does nothing against one at or below 0.
_Avoid_: lethality, pierce, shred

**Reduction**:
An effect on the target that lowers its Armor or Magic Resist for every hit while it lasts, below 0 too.
It belongs to the target, where Penetration belongs to a hit.
_Avoid_: shred, sunder

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
some Magic Resist, high XP. Like every enemy it is built from modifiable stats.
A swarm of them is a clump for the **Cast** to catch; it is what
an area magical build clears best, and what a single-target build gets overwhelmed by.
Parametric off the Location's source level.
_Avoid_: minion, add, runner, rusher, trash

**Origin Weight**:
The Behaviour Profile slider (0..1, default 0.5) that sets how strongly home pulls against nearness when the
hero picks a target. Each living enemy scores `weight × its distance from the Origin + (1 − weight) × its
distance from the hero`, and the lowest score is taken. 0 is a brawler who fights what is nearest him; 1 is a
homebody who never wanders after a straggler. It replaces any leash: nothing caps how far he walks. It only
steers a *new* choice - see **Sticky Target**.
_Avoid_: leash, aggro range, home pull

**Sticky Target**:
The one enemy the hero is fighting. He keeps it until it falls, so a fresh spawn never turns him round mid-run,
and walks until it is within his **Strike Range**, without overshooting. The exception is a target still out of
reach while another enemy stands inside it: he then switches to the best-scoring enemy inside (**Origin
Weight**), so he is never hit for free. With no enemy alive he walks back to the **Origin**. The arena view rings this
enemy. His movement speed is the `MovementSpeed` stat; his **Strike Range** is his weapon type's, not a stat.
_Avoid_: lock-on, focus target, aggro target, current target

**Cast Threshold**:
The `Resource` fraction the hero charges up to before it will start a run of Casts,
after which it Casts until the pool is drained to 0 (the last Cast takes the remainder),
then recharges back up to the threshold. At 0 that is purely regen-gated; at 100% it refills completely, then bursts the
lot. Low is a continuous, evenly-spaced
stream of Casts; high is the same Casts arriving clumped. A rhythm knob, not a power one
— the `/prototype` found it barely moves an Encounter's outcome against a continuous
spawn (ADR-0010).
_Avoid_: resource reserve, mana gate, burst threshold

## Screen Layout

**Minimap**:
The centre-screen element that always shows. Two visual states — Town and Field — each
with its own background art and button set. Drives panel open/close and Run transitions.
Its two sides are the **Town map** and the **Field map**: which one is up is where the hero is,
and what can be opened follows it — the Town Stops on the Town map, the **Combat Panel** and
the ground on the Field map, the hero's own panels on both — never the Run's state.
_Avoid_: world map (the **World** is the per-hero state that never saves), compass, hud map

**Exit**:
The button that leaves one side of the **Minimap** for the other, named for the side it is on:
the **Town Exit** (labelled To Fields) and the **Field Exit** (labelled To Town). A click on it
walks the **Hero figure** there and starts the crossfade at once, which takes input away until
the other side is up. The two Exits are not at the same place on screen. The Field Exit on a
live Run is a **Return**.
_Avoid_: gate, door, portal, entrance; "ToFields exit" / "ToTown exit" (say Town Exit / Field Exit)

**Waypoint**:
The point in Town where the **Hero figure** stands when it has not walked in — on entering the
game with **Play**, after a hero load and after a **Respawn** — and where a **Town Portal**
opens. After a **Return** the hero figure stands at the **Town Exit** instead, having come in
through it.
_Avoid_: spawn point, campfire, start, hearth

**Hero figure**:
The hero drawn on a side of the **Minimap**, one per side, each fading with its side. In the
Field it stands where the **Arena** has the hero while an **Encounter** runs, and **Travels**
otherwise.
_Avoid_: avatar, token, icon (the Hero icon is a Location's marker)

**Travel**:
A **Hero figure**'s walk outside an **Encounter** — to a **Town Stop**, an **Exit** or a
**Location** — at the hero's movement speed. It is only shown: what the click asked for has
already happened, and nothing waits for the figure to arrive. A new click turns it toward the
new place.
_Avoid_: walk (a **Walk** is an **Intent** inside an Encounter), move, path

**Town Stop**:
One clickable point on the Minimap's Town side — the **Stash**, the **Vendor** and the
**Healer** today. Exactly one is open at a time, each showing its own **Side Panel**. A
Town Stop is not a **Location**: Locations are the Field destinations a Run is **Sent** to.
A click opens the Stop at once; the hero figure walking there afterwards is only shown, and
nothing waits on it. A Town Stop authored as locked (the Vendor) is an **Unlockable** that never
Completes, opened by a Quest or Location reaching a state; the others are simply open.
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
The single-valued enum (`None`, `Hero`, `Stash`, `Vendor`, `Healer`, `Ground`) that answers two
questions at once: which panels are up, and where a **Quick Move** lands. Entry points
*request* a context; every panel *derives* its visibility from it. A context names the
**Hero Panel** plus at most one other panel (a Town Stop's, or the **Combat Panel**'s ground
tab for `Ground`, so that one needs the hero's bag open too; `Ground` is reachable in Town as well,
through its toggle), so the panel set is derived rather than
announced, and a panel that belongs to every context can never be the thing that names one
(ADR-0013). It is held by the **World** (reached through the inventory service,
ADR-0015) and read by the trade flow for **Quick Move**
routing. Its members are not homogeneous and need not be — the Stash is the player's
own storage, the Vendor is someone else's — because the only question the enum answers is
which context is active. Neither the Run nor the map is a member: the map that is up
*constrains* contexts. On the **Field map** only `None`, `Hero` and `Ground` are reachable,
and `None` there shows the **Combat Panel**, so closing the hero's bag brings it back. In Town
all of them are, `Ground` included, through its toggle.
_Avoid_: Side Panel Context (retired), trade target, active panel, current context

**Combat Panel**:
The left-side panel of the **Field map**: shown there on the `None` and `Ground` contexts,
derived like a **Side Panel**, never by the Run's state. Holds behaviour sliders, enemy
health bars, encounter stats and the ground tab. The ground shows only while the Run is
live; with no Run, or a held one, it is empty — a held Location's ground is seen by going
back there. In Town the ground is the Town ground, opened by the Ground toggle.
_Avoid_: debug panel, sim panel, fight panel

**Ability Hotbar**:
A centre-bottom bar of ability icons (Strike, Cast) that flash on use and show
cooldown overlays. Minimal v1 is flash-only; cooldown visuals are a follow-up.
_Avoid_: skill bar, action bar, power bar

**Ground Items List**:
A pooled list of Ground Items (one bound row each) reading the **Ground**, in age order, oldest first.
Each entry shows the item name and icon, supports hover preview and click-to-pick-up. One
entry per package, so a stack is one entry, not one per item. A click picks the **Drop** up
through the player's acquisition entry point, so a full bag leaves it lying there. The ground
is a grid container the size of the stash, and this list is its second view: both read the same
packages. A package that lands, or a stack that gains items, becomes the newest; when the ground
is full the oldest are removed until the new one fits, and nothing else is rearranged. It also
shows what the player dropped there — by Quick Move or by releasing a drag on the ground slot,
which evict the oldest the same way — and like any **Drop** it is gone when the Run ends. In Town the
list shows the same ground, reached with the Ground toggle beside the Hero toggle, and what lies there is
gone when the hero is **Sent**.
_Avoid_: loot beam, drop list, world items


## Front End

The screens around the game, as opposed to inside it.

**Main Menu**:
The first screen a launch shows, and where leaving a **Session** lands. Its **hero slots** and showcase
choose the **Hero**; its buttons lead into the game (**Play**) or to the screens around it (Settings,
the **Changelog**, Credits and the community links). Nothing on it is a Run, and no Run is ever
underway while it is shown.
_Avoid_: title screen, start screen, home

**Play**:
The Main Menu's way into the game with the selected **Hero**. On arrival the selected hero is the one
last played, so returning is one click. Not shown until a hero that can be read is selected. Its button says
what it does: "Start Game" for a **Draft** (the first time the hero enters), "Continue" for a hero that has a
**Hero save**.
_Avoid_: Start Run (a Run starts in Town, not here), Load Game

**Draft**:
A **Hero** the player has named on the Main Menu and not yet started. It sits in the **Hero slots** and shows
in the showcase like any hero, but it has no **Hero save** and the **Account** does not know it: **Play**
writes its first save, and closing the game, deleting it or naming another hero before then loses it. There
is at most one.
_Avoid_: unsaved hero, pending hero, new hero

**Hero slots**:
The row of **Hero saves** on the Main Menu, one slot per hero and one to create another. Selecting a
slot loads that hero for show and enters nothing: its gear, name, level and class appear, and so do the
**Location** it will next be Sent to (blank before its first Send), its **Corpse** and where it lies,
its total **Kills**, and the average item level of the gear it wears (coins and empty slots aside).
**Play** is what enters the game. A save that cannot be read keeps its slot, says why, and can only be
deleted. Creating a hero takes a name and nothing else, and makes a **Draft**; deleting one asks first,
because the file is gone for good.
_Avoid_: Heroes screen, character select, profile screen, save slots

**Pause**:
The player freezing a Run in the Field so it stops advancing. Refused in Town, where nothing advances.
_Avoid_: stop, freeze (the Pause Menu does that without being a Pause)

**Pause Menu**:
The in-game menu **Cancel** opens when there is nothing left to dismiss: Resume, Settings, **Save &
Exit** and Quit Game. While it is open the Run holds still, and closing it gives back whatever Pause
state the player had, so opening and closing it never changes the player's own Pause. Available in
Town and in the Field.
_Avoid_: Pause (that is the player's freeze), escape menu, options

**Cancel**:
The player's way to back out, one layer per press: a Package held on the cursor goes back, then an
open **Popup** closes to the one beneath it, then the **Inventory Context** returns to `None`
(every panel closes at once), and only then does the **Pause Menu** open. On the Main Menu it steps
back out of a screen and does nothing at the root.
_Avoid_: Escape (that is the usual key), close, back

**Save & Exit**:
Leaving the **Session** for the Main Menu. With a Run live or held it first ends it as a **Return** does, so
everything the hero holds is kept and there is no Death penalty, then writes the **Hero save**. The
player is asked to confirm only while a Run is live or held. It is the one action that tells the player their
progress is safe to leave.
_Avoid_: quit, exit (the Main Menu's Exit closes the application), abandon

## Changelog

**Changelog**:
The player-facing record of the game's progress, reached from the main menu. One document with
three sections — **Patch Notes**, **Known Issues** and the **Roadmap** — written in player
language, with no ticket numbers or internal terms. It is the only place those three lists
live; the private idea backlog (the **Icebox**) is a separate thing the player never sees.
_Avoid_: update notes, release notes, news

**Patch Notes**:
The Changelog section of what changed, one entry per released version (or per dated milestone
for builds that predate version numbers), newest first. An entry is the only place a fixed
**Known Issue** or a shipped **Roadmap** item goes to.
_Avoid_: changelog (that is the whole document), release log

**Known Issue**:
A bug or shortcoming the player can hit today and that the game has not fixed. It leaves the
list when fixed and is then recorded as a **Patch Notes** entry.
_Avoid_: bug, TODO, defect

**Roadmap**:
The Changelog section of features the game intends to add. An item leaves it when it ships and
is then recorded as a **Patch Notes** entry. An idea is not on the Roadmap until it has been
reworded for players and moved out of the **Icebox**.
_Avoid_: planned features, backlog, wishlist

**Icebox**:
The private backlog of ideas that are not committed to. Never shown to the player.
_Avoid_: ideas list, backlog

## UI Components

The Submodule provides basic components to reuse or derive from.

**Interactive Element**:
It reacts to pointer handler to provide visual feedback. Base class for buttons and toggles.

**Panels**:
A panel is a parent component that groups content. It provides appearance options such as fading in and out, scaling and movement. A panel should always stay enabled, only its alpha is set to 0. Only a `SimplePanel` is called a panel: a component that groups content and is shown by being enabled is a **Popup**, and one that only lists or lays out is a list, a sheet or a layout.

**ExclusiveGroups**:
A collection of mutually exclusive Toggles or Panels of which at most one is active at a time. 
"Activate" deactivates whichever sibling held the slot. 

**Two-panel switch**:
A pair of tab buttons that show one of exactly two panels. One toggle, the **peek toggle**, owns a
single bool and the panels follow it: one shows while the bool is on, the other while it is off.
The other button is its companion, named by the peek toggle's `companionToggle`: the one other toggle
in the same `ToggleGroup`. The group holds exactly the pair; a third member is not supported.
The group forbids switch-off, so a click on either button flips the bool and the group does the
switching; the pair is scene layout, with the two panels in a `PanelGroup` of their own. The Vendor
and the Healer use it for their Supply and Sold tabs, and the group's first member is the tab a
panel reopens on. A tab pair that is not a two-panel switch is a plain group of `PanelToggle`s,
which can hold any number of tabs and cannot say "the other one".
_Avoid_: tab group (for a pair), tab toggle pair, mirror toggle, driver

**Peek**:
Holding Tab on an open selling panel (the Vendor, the Healer) shows the other tab of its
**Two-panel switch**; letting go lands back where the player was. A real selection through the group,
not a view-only overlay, so both buttons follow. It is the **peek toggle**'s own behaviour: a subclass
names the key (`SellingPanelPeekToggle` reads `GameAction.PeekTab`, not the tooltip's `GameAction.ShowDetails` on
Alt: Alt over an item expands its tooltip, and a tab swap would take that item away; `PeekTabKey` also
ignores a Tab that went down while an Alt key was held, which is the window switch, while Tab then Alt keeps the
peek; it reads the physical keys, not the actions bound to them), and the
peek counts only while the toggle is reachable, so a closed or hidden panel never peeks. It begins
and ends once per hold. A press remembers whether the peek toggle was on; on release the peek is given
back only if that has changed, so a click on either tab during the hold leaves the player where they
clicked. A click on the tab being peeked at is refused and changes nothing. A panel closing during a hold gives the peek back at once, before its group
resets. A peek touches the bool and nothing else: not a sale, a drag, a purchase or the **Inventory
Context**. A sale never switches the tab.
_Avoid_: preview, hover tab, tab flip

**Displays and Views**:
A display is the visual representation of a data object, and is either pushed or bound. A *pushed* display
implements `IDisplay<T>`: `Repaint(T)` redraws it from the data it is handed on a data change. A *bound*
display follows a source it was given (`Bind`/`Unbind`) or resolves itself (a container display binds by its
role) and redraws on that source's events. It implements no interface, because the bindings share no signature
and `Bind` is a subscription with a lifetime, not a draw. A component that is neither pushed nor bound is not a
display. This differs from views, that show static data (an item's name, icon and colour; the build version).
_Avoid_: calling a list, a text or a layout a display unless it is pushed or bound

**Popup**:
A screen opened over the one beneath it, which **Cancel** closes. It is shown either by being enabled, so it
resets each time it opens (the hero name entry, the delete and Save & Exit confirmations, the entry notice, the
**Pause Menu**), or as a **Panel**, so it fades (the **Changelog**). Each registers a Cancel layer while it is
open: the **Pause Menu** on its own rank, every other Popup on the Popup rank. A Popup that asks for a choice opens with
one button selected, which Enter presses: the yes option, unless it would delete or discard something,
when it is the no option (the hero delete and **Save & Exit** select their way back).

**Notice**:
A **Popup** with exactly one button, because there is nothing to choose: the button is selected when
it opens, and Enter and **Cancel** both press it. The **Death** notice (its button is **Respawn**) and
the entry notice are Notices.
_Avoid_: message box, alert, dialog, toast
_Avoid_: modal, dialog, prompt, overlay (that names the scrim, and not every Popup has one)