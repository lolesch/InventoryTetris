/// KNOWN ISSUES:
// cant rightclick equip a 2H when there is an offhand but no mainhand equipped
// autoequipping a 2H will unequipp offhands if there is no mainhand equipped

// Droping an item selects unexpected positions

// comparison shows wrong numbers of first hover => rehover shows correct values
// comparison cant compare against all equipment of same type (i.e. both rings)

// modified affix range can result in higher min than max values => reorder before setting the range

/// TODO
// fix health affixes dont add to health as resource

// add a source to statModifiers to remove all modifiers of that source 

// fix characterStats modifier list => hovering items (previewDisplay) adds modifiers without equipping and never removes them

// fix the item drop positioning
// -> the offset is fine once the drop includes the items dimensions
// atm. the item is dropped in the surrounding slots dependent on the mouse offset to the center of the hovered slot.
// this offset needs to be calculated in relation to the center of the drag display

// hoverable playerStats => tooltip baseValue and mod sum per mod type

// rework item comparison
// compare rings and weapons to both slots

/// ITEM EQUIPMENT LOGIC
// cant rightclick equip a 2H when there is an offhand but no mainhand equipped
// autoequipping a 2H will unequipp offhands if there is no mainhand equipped

/// COMBAT SIMULATION
// simulate Dummy to take damage
// Weapons should have attackSpeed multiply mod
// Weapons should set Damage base value on equip

// editable Dummy CharacterStats

// lokalPlayer take damage
// HUD to show health

// add skills to deal damage with
// HUD to show skills
// HUD to show Resource

/// CRAFTING SYSTEM
// adjustable affixe amount
// adjustable affixes
// adjustable affixe values
// adjustable item rarity
// lock affixes
// ...

/// CRAFTABLE SKILLS
// base skills have a set of mods
// crafting allows to adjust, add or combine mods
/* sample mods are:
 * damageType
 * delayTime
 * radius
 * projectileAmount
 * ... */

/// ITEM SOCKETS
// implement the simplest version of socketing you can come up with
// design socketables - this goes into attribute design

/// ICEBOX

// Improved MagicFind:
// Effective MF = (MF * Factor) / (MF + Factor), where Factor=250 for unique items, 500 for set items and 600 for rare items..

/// POLISH

///GUSTAV
/*
 * 
 */