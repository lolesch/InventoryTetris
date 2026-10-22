# Location unlocks - a Send fields a Population, and Locations, Quests and the Vendor open through one Unlock graph

Date: 2026-10-16
Status: Spec, ready for `/to-tickets`. Settled by the wayfinder ticket "How does a Location fill, clear and
unlock?" (lolesch/InventoryTetris#294, on the map "Wayfinder: MVP vertical slice", #293) through `/rederive`
and `/drift-review`; the resolution and its amendments are on the ticket. Builds on the hero-travel and
Town Portal specs (`2026-10-16-hero-travel-design.md`, `2026-10-16-town-portal-design.md`, ADR-0021,
ADR-0022), which are merged. Records ADR-0023 and amends ADR-0010.

## Problem Statement

The hero cannot lose and the player has nothing to aim for. A Location runs Encounters endlessly at a fixed
difficulty, so a Run only ends when the player leaves or the hero dies. Every Location is open from the
start: a new hero walks straight into Ashfall, levels on its XP, outgrows its enemies, and farms forever. No
Location is ever finished, so nothing is ever earned by finishing it. The Vendor is locked by a scene setting
that nothing in the game can lift. There is no word, state or saved fact for "the hero has done this", so
there is no progression to build a difficulty curve on.

## Solution

A Location holds a fixed number of Encounters, its **Population**, rolled fresh on every Send. When the hero
clears the last one, the Location is **Complete**: the fight stops, the hero stands where he is, and the
player chooses what next - walk out (a Return), open a portal (a Recall), or click another Location.

Completing things opens things. Locations, Quests and locked Town Stops are **Unlockables** in one graph.
Each one names what it **Unlocks** when it becomes Active or when it becomes Complete. Thornwood is open on a
new hero; Completing Thornwood opens Ashfall. A Quest, "Free the merchant", is Active from the start and
Completes when Thornwood does, which opens the Vendor. The player sees the next Location greyed out until it
opens, and nothing beyond it. Every Location stays farmable after it is Complete: a Send repopulates it.

## User Stories

1. As a new player, I want only the first Location to be open, so that I start where the game is tuned for me.
2. As a new player, I want to see the next Location greyed out, so that I know there is somewhere to go and what opens it.
3. As a new player, I want Locations two steps away to stay hidden, so that the map grows as I progress instead of showing everything at once.
4. As a player, I want a greyed-out Location to do nothing when I click it, so that I cannot be sent somewhere I have not earned.
5. As a player, I want each Send to a Location to field a fixed number of Encounters, so that a Location is something I can finish.
6. As a player, I want that number to vary a little from Send to Send, so that two trips to the same Location do not feel identical.
7. As a player, I want to see "Encounter 3 of 5" while I fight, so that I know how far I am from finishing the Location.
8. As a player, I want the fight to stop when the last Encounter is cleared, so that finishing a Location is a clear moment.
9. As a player, I want my hero to stay where he stands after a Location is Complete, so that I can pick up the loot lying around him.
10. As a player, I want the Location's next Location to open the moment I Complete it, so that I can move on without going to Town first.
11. As a player, after a Complete I want to choose between a Return, a Recall and clicking another Location, so that I decide whether to bank, heal or push on.
12. As a player, I want a Return after a Complete to be free and a Recall to cost a Portal Scroll as always, so that the choice has a cost I understand.
13. As a player, I want to click a Complete Location again and get a fresh Population, so that I can farm it for loot and XP.
14. As a player, I want a Complete Location to stay Complete forever, so that nothing I have earned is taken back.
15. As a player, I want a Location I opened to stay open after a Death, so that I can go back and recover my Corpse.
16. As a player, I want a Recall in the middle of a Location to keep my progress through its Population, so that a portal trip to heal does not cost me the Location.
17. As a player, I want clearing the rest of a Location after a portal trip to still Complete it, so that the portal is a pause and not a restart.
18. As a player, I want a Return or a Relocate in the middle of a Location to start it over next time, so that leaving without a portal has a cost.
19. As a player, I want to Relocate only to Locations that are open, so that the map's rules hold mid-Run too.
20. As a player, I want the Vendor to open when I free the merchant by Completing Thornwood, so that the shop feels earned.
21. As a player, I want the Stash and the Healer open from the start, so that I can always store loot, heal and buy Portal Scrolls.
22. As a player, I want a Quest to count only what I do after it became Active, so that a Quest asks me to do something rather than ticking itself off.
23. As a player, I want a Quest that becomes Active by the very Complete it asks for to wait for the next one, so that the rule has no exception.
24. As a player, I want my Completes saved the moment they happen, even mid-Run, so that closing the browser cannot take a Location back.
25. As a player, I want a hero I load to show exactly the Locations and Town Stops that hero has opened, so that every hero has its own progress.
26. As a player, I want a held Run resumed through the portal to show its Location selected without starting a new fight, so that the Field map matches where my hero is.
27. As a level designer, I want to author what each Location and Quest Unlocks on the Location or Quest itself, so that I author progress the way a player experiences it.
28. As a level designer, I want an Unlockable listed by two sources to open when either one gets there, so that I can branch the map without a new mechanism.
29. As a level designer, I want an Unlockable no source names to be open from the start, so that a new root needs no extra flag.
30. As a level designer, I want a link that opens something while its source is Active, as well as one that opens it once the source is Complete, so that a Quest can open content for as long as it is underway.
31. As a level designer, I want a loop in the links, an Unlockable no root leads to, or a Town Stop that claims to Complete to fail loudly when the game loads, so that a broken graph never ships silently.
32. As a level designer, I want to author a Location's Population as a range, so that tuning a Location's size is one field.
33. As a developer, I want "Active" in a rule to mean Active or Complete, so that a Complete source never re-locks what it opened.
34. As a developer, I want only the Completed set saved and every other state derived from it, so that the saved state cannot contradict itself.

## Implementation Decisions

- **The Unlock State** is an ordered enum: `Hidden < Locked < Active < Complete`. A state only moves forward.
  A rule that asks for Active is a `>= Active` comparison, so a Complete source still satisfies it.
- **An Unlockable** is the authored base shared by every Location, every Quest and each Town Stop that starts
  locked. It carries a stable id (the one a save names), and two lists: what it Unlocks when it reaches
  Active, and what it Unlocks when it reaches Complete. Links are authored on the unlocking side. A Town Stop
  that is always open (the Stash, the Healer) is not an Unlockable.
- **Deriving a state** for one hero, from the authored graph and the hero's Completed set:
  - Complete if its id is in the Completed set;
  - else Active if no link names it (a root), or if any link naming it is satisfied (its source is at least
    Active, for an Unlocks-when-Active link, or Complete, for an Unlocks-when-Complete link);
  - else Locked if any source naming it is at least Active (shown greyed out);
  - else Hidden.
  Several sources are an OR. There are no AND gates.
- **A progression registry** builds from the authored Unlockables in the game config. It indexes them by id,
  builds the reverse links (what names each Unlockable) once, and answers the state of any Unlockable for a
  hero. It throws at build time on a duplicate id, a loop, an Unlockable no root reaches, and a Town Stop with
  Unlocks-when-Complete links (it never Completes). The Location registry's existing duplicate-id check is the
  model.
- **The hero's Completed set** is a set of Unlockable ids, owned by the Hero and saved in the Hero save as a
  list of ids. An id the registry no longer knows is dropped on load. There is no migration: no saves exist
  that predate it.
- **A Location's Population** is authored on the Location as a `[min,max]` count of Encounters and rolled on
  every Send and Relocate (and on a portal used after a relaunch, which Sends). A resume through the portal is
  not a roll: the held Run keeps its Population progress (Town Portal spec).
- **The Encounter simulation** knows its Population. After a clear, while Encounters remain, it beats and
  begins the next, as today. After the last clear it enters a new phase, Complete: nothing spawns, the
  hero's Brain does not walk him home, he stands where he is, and the cadences stand still. It raises a
  Location-completed signal once. It exposes the current Encounter's index and the Population size for the
  "Encounter 3 of 5" readout, which replaces the "Encounter N, cleared M" line. A Recall, a Return, a
  Relocate and a Death work from the Complete phase as from any other.
- **The simulation service** refuses a Send or a Relocate to a Location that is not Active for the hero, the
  way it refuses other illegal Sends: nothing changes and the Run is untouched. This is the one place the
  rule is enforced; the Location toggles only reflect it.
- **On a Location Completing**, the service applies, in order: (1) it computes which Quests are Active now,
  before anything changes; (2) it adds the Location to the Completed set; (3) every Quest that was Active in
  step 1 and whose objective is that Location Completes too, and is added; (4) it repeats step 3 for Quests
  whose objective is a Quest just Completed, until nothing changes; (5) it saves the hero, writing what a quit
  at that moment would; (6) it raises one progression-changed signal. Because states are taken before the
  event, a Quest that becomes Active through this very Complete does not count it.
- **A Quest** is an authored Unlockable with one objective: another Unlockable's id, met when that one
  Completes while the Quest is Active. Objectives of other kinds are out of scope.
- **The Location toggles** read their state from the progression registry: Hidden ones are not shown, Locked
  ones are shown and not interactable, Active and Complete ones work as today. They refresh on the
  progression-changed signal and on a hero load. A locked Town Stop toggle (the Vendor) does the same.
- **Recall after a Complete is allowed.** It spends a Portal Scroll and holds the Run in its Complete phase;
  the portal resumes into the emptied Location.
- **Saving on Complete** goes through the hero save service like any other save, with its existing failure
  handling. When the hero saves in general is the ticket "When does the Hero save?" (#310); this spec adds
  the one trigger.
- **The authored MVP graph:** Thornwood (root) Unlocks Ashfall when Complete. The Quest "Free the merchant"
  (root) has the objective Thornwood and Unlocks the Vendor when Complete. The Stash and the Healer are not
  Unlockables. The Vendor's scene lock is removed in favour of the Unlockable.
- **Retreat is already gone** (hero-travel spec): a Run ends only by a Return or a Death, so a bag filling up
  never ends one. Nothing in this spec ends a Run on its own.
- **ADR-0023** records the two hard-to-reverse choices: a Send fields a finite Population (superseding
  ADR-0010's "endless Encounters, no cap"), and progression is one Unlock graph over Locations, Quests and
  locked Town Stops rather than Quests alone. ADR-0010 gains an "Amended by ADR-0023" note.

## Testing Decisions

- Tests pin behaviour a player or a level designer could observe through the seam, not the shape of the
  registry or the order of internal calls.
- **The simulation service over a test game** (the existing test-game fixture, which already authors
  Locations into a config) is the main seam: a Send and a Relocate refused for a Hidden or Locked Location;
  a Population cleared, so the Location Completes and its id joins the Completed set; Ashfall going from
  Locked to Active on Thornwood's Complete; the Vendor opening when "Free the merchant" Completes; a Quest
  that becomes Active by the same Complete it asks for staying Active; the derived states for a root, a
  Locked successor and a Hidden second step; the save written on Complete through the existing save fake; a
  Recall allowed from the Complete phase; the registry's build errors (duplicate id, loop, unreachable,
  Town Stop with Unlocks-when-Complete). Prior art: the simulation service tests and their test-game fixture.
- **The Encounter simulation** (pure, Unity-free): the Population rolled within its range; no Encounter
  begun after the last; the Complete phase entered once, with the signal raised once and the hero standing
  still; the index and size for "Encounter 3 of 5"; Recall, Return, Relocate and Death from the Complete
  phase. The tests that pin the endless loop today (the arrival-delay test's "next Encounter after the beat",
  the Encounter lifecycle tests, the Relocate tests) are rewritten in the same ticket that removes the loop,
  not in a follow-up.
- **The Hero persistence round-trip:** the Completed ids save and restore, and an unknown id is dropped.
  Prior art: the existing Hero persistence tests.

## Out of Scope

- The quest log Side Panel, a Quest-complete notice, and a Population progress bar (all M2 on the map).
- Quest objectives other than "an Unlockable Completes" (kill counts, items), and Quest rewards.
- AND gates between sources.
- A Location's size beyond its Population count: a varying Arena per Send, procedural layouts.
- Enemies wearing their loot (map: out of scope).
- Saving on every Hero change; that is "When does the Hero save?" (#310).
- Migrating saves that predate the Completed set; there are none.
- Balance numbers: each Location's Population range, how hard a Complete is. That is the ticket "Balance
  sweep: time to kill and time to die per Location and hero level" (#296), which counts how many Sends a
  Complete takes per gear level and models the Fail Bucket; a Complete does not have to be reachable on the
  first Send.

## Further Notes

- `/drift-review` over the existing slice found: ADR-0010's Run-scale bullet and the endless-loop tests as
  residue to retire with this swap (above); the Town ground ticket (#285) building on the Ground Items List
  that "Loot in the Arena" (#307) plans to remove, so #285 waits for #307 or moves to the floor grid; and the
  gaps now decided above (Population vs the bag, Relocate between Active Locations, save on Complete, the
  progress readout, no migration).
- Relocating between Active Locations without visiting Town is allowed. Loot progression, selling, Restocks
  and storing finds are the reason to go back, not a forced end.
- The Combat Panel's encounter stats are where "Encounter 3 of 5" shows, on the Field map (Town Portal
  spec's scope table).
