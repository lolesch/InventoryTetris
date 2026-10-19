# Patch Notes

## Unreleased

- Locations now open as you progress: Thornwood is open from the start and clears the way to Ashfall, and the Vendor opens once you free the merchant. A Location ends after its encounters, and the screen shows how far along you are (for example "Encounter 3 of 5").
- The game now starts on the main menu: pick a hero or create one, then press Play.
- New heroes are given a name, and a hero can be deleted after a confirmation.
- The Cancel key steps back one layer at a time, and with nothing left to close it opens the Pause Menu.
- The Pause Menu holds the game still and lets you resume, or save and exit back to the main menu.
- The main menu shows the build version in the corner.
- A Changelog button on the main menu opens Patch Notes, Known Issues and the Roadmap.
- The Changelog closes with the Cancel key, and its text scrolls instead of running off the panel.
- A new hero has to be named, and is saved the first time you press Start Game; after that the button reads Continue. Play and Delete only appear once a hero is chosen.
- The main menu shows the chosen hero's name, class and level.
- Fixed the health and resource bars not showing a higher maximum after equipping gear while the game is paused.
- Hold Tab over an open Vendor or Healer to peek at its other tab; Alt still shows more detail on an item.
- The hero in the field now has a health bar that shows its shield, and flashes and shakes when hit, like the enemies do.
- Your hero now stands on the Town map and walks to whichever Town Stop you open, by click or hotkey; faster boots make him walk faster.
- The hero's walks on the Town and Field maps now follow the speed slider, and carry on while the game is paused.
- The hero no longer recalls to town on his own, so the Retreat HP and Recall Bag sliders are gone; a Run ends when you return him or he dies.
- Leaving Town through the Town Exit now shows your hero walking towards it while the Town map fades slowly into the Field map.
- Picking another Location mid-fight sends the hero walking there from where he stood, and the enemies he left behind fade out.
- Your corpse now lies where your hero fell, on the Field map, before you send him back for it.
- When your hero dies, a notice tells you where he fell, what it cost and that your gear, bag and coins lie there; Respawn (button, Enter or Cancel) brings him back to town at full health.
- A death now takes everything your hero wore and carried, coins included, and he respawns bare; recovering the corpse puts each item back where it was, and a bag item no longer equips itself.
- When your hero falls, the last damage numbers now drift by in slow motion for a moment.
- Leaving the Field through its Exit now shows your hero walking out while the Field map fades; in Town he comes in standing at the Town Exit.
- When your hero falls, he lies where he fell while the fight stands still, then your corpse appears there and the death notice opens.
- Town now has a ground: shift-click an item in your bag, or drop it on the ground slot, to leave it there, and click it in the Ground list to pick it up. A full bag leaves it lying, and it is gone when you Send the hero out.
- Selling from a full bag no longer fails: the bag keeps the gold and silver first, and the coins that do not fit lie on the Town ground for you to pick up.
- Consolidate now also puts your coins in order, gold first and iron last, even when they were already in the biggest denominations.
- A coin you drop or shift-click into your bag now falls into that order too, so shift-clicking your coins to the Stash one by one fills it gold first.
- Recall the hero from the Field with B, the Recall button (it shows how many Portal Scrolls you have) or a right-click on a Portal Scroll. A Town Portal opens beside him, the Run is held where it stands, and he keeps regenerating while it waits.
- Step through the Town Portal to carry on with the held Run, or to be sent to the Location again when none is held. The portal stays open until you use it or recall again.
- Survival now comes from your gear: levelling up grows your health, resource and damage a little but no longer heals you, and a new hero no longer regenerates health on his own, so health regeneration has to come from items. The Healer still refills you for free.

## 0.0.5_Alpha - 2026-09-25

- The hero now fights on its own in the field, using Strike and Cast against groups of enemies.
- Send the hero out to a location, recall them to town, and see what they bring back after a run.
- Two locations to venture into: Thornwood and Ashfall.
- Defeated enemies drop loot, coins and experience, and a fallen hero leaves a corpse holding their bag.
- Healers now refill health and resource, and also do so when the hero levels up.
- New combat screen with enemy health bars, an encounter stats panel and a flashing ability hotbar.
- New map with Town and Field views that handles sending and recalling the hero.
- Vendors can buy and sell items, with a sell basket to stage, preview, confirm or cancel a sale.
- Items are bought when you drop them, not when you pick them up.
- Coins carry the rarity of their denomination.
- Magic find now makes rarer items more likely, and currency drops come as piles.
- Quick move sends an item to the panel that is currently open.
- Dragging an item can be cancelled to put it back where it came from.
- Swapping an item in place puts the displaced item in the vacated spot.
- Fixed two-handed weapons being lost or duplicated when swapped on the equipment slots.
- Fixed the hover preview and the drop highlight showing the wrong item or the wrong cells.
- Fixed side panels and town buttons not closing or reacting to hotkeys correctly.
- Fixed the map sending the hero twice.
- Fixed hero sliders that did nothing.
- Fixed items being lost when an action could not be completed.

## 2023-10-15

- Dropping items no longer duplicates items they overlap.
- Items land in a better position when dropped.
- Items are protected from being lost when the inventory is full.
- Two-handed weapons and a reworked equipment system.
- Equipping and unequipping items now adds and removes their stat bonuses.
- Item values and currency are shown.
- Inventory sorting and item movement are improved.
- New item icons, with icons flipped to match the equipment layout.
- Added a health bar, a shield, experience and levelling up.
- Item stats are displayed more clearly, and item bonuses combine more sensibly.
- A first draft of vendor and trade screens.
- Smaller textures for faster loading in the browser version.

## 2022-01-23

- First playable build in the browser.
- Drag and drop items between inventory slots.
- Equipment slots work with the inventory, and right-click equips or unequips an item.
- Equipped items get random colours so similar items are easy to tell apart.
- Button tooltips.
- New item icons and better item placement.
- Dropping and removing items works without errors.
- First attempts at mobile support: touch input and screen rotation.

# Known Issues

- The item comparison shows wrong numbers for the item that is not equipped.
- Rings and weapons are not compared against both of their slots, including two-handed weapons.

# Roadmap

- A dedicated loot filter, more precise than the rarity slider.
- Deeper item design, with more interesting choices than bigger numbers.
