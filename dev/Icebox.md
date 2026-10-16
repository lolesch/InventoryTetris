# Ideas

### Item Crafting

#### Recover a bricked item as a rare mechanic. imagine a altar you can insert an item and have a soft recover/revert the bricking, that would allow for another high roll. The cost would still be the high roll itself.

### Location Discovery/Unlock
the world map is reveled over time. Mechanism has to be designed. with some sort of random factor mimicking different map layouts and therefore different discovery timings...
Same goes for the town hub. Should everything be available from the get go, or evolve over time? again think magiCraft here to open Meta options over time.

### Distribution and Card Deck
check if the distributions should include the card deck for consistent drop chance.

### ItemDesign
This is a bigger epic that includes many things. The goal is to design interesting item choices with more depth than 'number goes up'.
#### Affixes
define a set of stats -> affixes that define the character. then define on what equipment slots these affixes could appear and in what range.
##### Affix Balancing
Give each affix a rough value so items become comparable and spikes can be detected. 
### Item Sandbox
a player facing item catalogue with a dummy character to equip. For theory crafting and filtering. Think BackpackBattles or Magicraft for reference.

### Rework Experience
Experience should not be a resource at all. we instead should have an experienceGainBonus stat. This links to more design on what a level actually is or wnats to express.

### Pause Menu
pauses inGame time by stopping ticker time propagation (corrected: a hold on the simulation beside the player's Pause, never `Time.timeScale`; see `dev/specs/2026-10-12-front-end-flow-design.md`). Gives access to return to the main menu, quitting or returning to the game.

### Hardcore Mode
a hero mode chosen at creation, softcore (today's Death: a Corpse to recover) or hardcore. What a hardcore Death does is the open question: the hero is deleted, or kept as a read-only grave, or retired to a hall. It breaks ADR-0009 ("Death is not a game-over") and ADR-0008 rules out a results screen, so it needs a grilling of its own, a save field and a Death flow that writes the file. The Heroes screen (front-end flow spec, 2026-10-12) leaves room for a mode on a row but shows none.

### Waypoints at Locations
A waypoint at a Location lets the hero Recall for free while he stands at it, without spending a Portal
Scroll; Town already has one (the Waypoint the Town Portal opens at). The scroll, `B` and the Recall itself
moved into the Town Portal spec (`dev/specs/2026-10-16-town-portal-design.md`); only the free waypoints at
Locations stay here. Raised in the 2026-10-16 hero-travel grilling.

### Corpse per Location
More than one Corpse: dying at Location 2, Respawning, then dying at Location 1 leaves two Corpses, each
shown where the hero fell, instead of the second Death destroying the first (ADR-0009). Open: a cap (one
per Location, a second Death there destroying the older one) or none, which would make dying again
cost nothing. Maybe recovery becomes a click on the Corpse that Travels the hero there, instead of the
pick-up on entering the Location. Changes the Hero save's one Corpse into a list. Raised in the
2026-10-16 hero-travel grilling and deferred; today's one-Corpse rule stands.

### Stock that never runs out
Some Supply items are bought without leaving the Supply: the purchase hands over the item and the slot
stays filled (not removed at all, or restocked at once). The Healer holds both kinds (items that vanish
when bought, and ones that never run out); the Vendor only the first; a later Gambler only the second,
offering unidentified equipment, so this is not a consumables-only rule. Raised in the 2026-10-16
hero-travel grilling.

### Books as an XP source
Books leave the consumable loot as a rarer drop for now; their use belongs to the effects work, as an
experience source or alike. Raised in the 2026-10-16 hero-travel grilling.
