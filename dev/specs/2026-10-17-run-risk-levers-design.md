# Run risk - the hero's survival comes from gear, and leaving a fight takes a moment

Date: 2026-10-17
Status: Settled in the 2026-10-17 grilling of "What makes a Run risky?" (#295, map #293). The numbers are
placeholders the balance sweep (#296) started and the re-sweep (#349) fixes. The Recall's opening lives in
`dev/specs/2026-10-16-town-portal-design.md`; land that spec with or before this one.

## Problem Statement

The hero cannot lose. A level-up refills Health and Resource, and XP arrives per kill, so a level-up mid-fight
is a free full heal at the worst moment for risk. A new hero regenerates 3.5 Health a second at base, in
Town and in the Field, which outheals the first Location's enemies. Death (ADR-0009) exists and never
happens.

## Decisions

**The hero's survival is the loot's job.** The game is a loot simulation, so the lever is enemy strength
against the hero's equipment. A hero with nothing equipped fails at a Location; the same hero in good gear
from that Location's drops clears it.

**A level-up grows the hero, it does not heal him.** No Health or Resource refill. Each level adds a small,
fixed set of base increases from one growth table, the same for every hero, kept small next to gear so the
loot stays the main lever. Attribute points to spend (Diablo II) are not part of this: they wait on what a
hero class owns and the stats sheet panel (both M2).

**No base Health regeneration.** A new hero's Health Regeneration is 0. Regeneration, and later Life on Kill
and Life Leech, come from gear affixes, as do the defense layers (Armor, Magic Resist, Shield). Shield
regeneration reads Health Regeneration, so it is 0 without gear too. Resource regeneration is untouched.

**Enemies never regenerate.** Damage dealt stays dealt, across a Recall too. Already true in code; now a
rule, so an enemy affix pool must leave regeneration out. Rare enemies and bosses are the exception, decided
later from the balanced normal enemies.

**Town healing stays free; leaving is the risk.** The Healer's full heal stays free and Portal Scrolls stay
common and cheap, as in Diablo II: a trip to Town costs time not spent farming. What a Recall risks is its
opening: the portal takes about half a second of sim time to open while the hero keeps fighting, and a Death
in that window is a Death (town-portal spec).

**The ladder.** A Location drops items across an item-level range whose top sits above its source level, so
its best drop is ahead of the area: the item that carries a hero into the next Location comes from farming
this one (#296). A harder Location pays more XP per kill through its archetypes' XP curves over source level
(the ±1%-per-level term stays as it is). Item level caps how far a Location's loot carries a hero, with
neighbouring bands overlapping: the best Thornwood gear can
clear Ashfall, typical Thornwood gear cannot. Ashfall is a gear-gated **step**, not a wall: a hero fresh
from Thornwood survives part of it, Returns before dying, and grows on Ashfall's own drops. The range runs
from the source level to two above, weighted toward the bottom, and item level scales every rolled value by
one formula ("What each Rarity is for", #303, `dev/specs/2026-10-18-rarity-roles-design.md`).

**What the sweep aims at (#296).** A careless new hero (no Recall, no potion) dies in Thornwood before its
first Send Completes; a played one Completes it. The first Location fields mainly slow, short-range enemies
(Brutes); later Locations mix in range and speed (Skirmishers) and scale Health. The sweep answers the
difficulty curve over synthetic Locations (source levels 1-10, mixed Rosters and Spawn Profiles, the two
existing archetypes) run headless; M1 still authors only Thornwood and Ashfall.

## User stories

1. As a player, levelling up mid-fight makes my hero a little stronger but does not refill his Health.
2. As a new player, my hero does not regenerate Health on his own; a regeneration affix on gear does.
3. As a player, an enemy I wounded stays wounded until it dies, even if I Recall and come back.
4. As a player, a Healer visit still heals my hero fully for free.
5. As a new player, Thornwood can kill my hero if I ignore his Health, and I can Complete it if I Recall or
   drink a potion in time.
6. As a player, Ashfall is too hard in my Thornwood gear to clear at first, but I can fight part of it,
   Return, and get stronger on what it drops.

## Implementation Decisions

- `Hero.GainExperience` stops calling `Heal()` on a level-up. `Heal()` stays: the Healer, the Respawn and
  the starter kit use it.
- `LevelProgression` gains the growth table: per level reached, flat `StatModifier`s on a few stats (Health,
  Resource, Physical and Magical Damage to start; the sweep picks the stats and values). The level-up path
  and `RestoreLevel` both apply it, so a loaded hero carries the modifiers of levels 2..N exactly as the XP
  threshold modifiers already do; the save still stores only the level.
- `DefaultHero.asset`: Health Regeneration (`StatName` 12) base 0, from 3.5.
- No change to `Enemy.Regenerate` (already a no-op); its comment names the rule.
- Tests (EditMode): a level-up leaves Health and Resource where they were and adds the growth modifiers; a
  restored level carries the same modifiers as a climbed one; a new hero at 0 regeneration does not heal over
  time.

## Not part of this

- The numbers: growth values, enemy curves, XP and the opening's duration (#296).
- Life on Kill, Life Leech and other heal stats (wait on the effects model).
- A third enemy archetype (its own decision ticket), rare enemies and bosses, authored Locations past
  Ashfall, attribute points.
