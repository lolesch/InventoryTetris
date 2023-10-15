/// KNOWN ISSUES:
// Dropping an item selects unexpected positions => was improved but does not support most overlapping

// comparison shows wrong numbers on the unequipped item
// comparison cant compare against all equipment of same type (rings, both weapon slots)

#region TODO
// gitPage for WebGL build

// fix the item drop positioning
// -> the offset is fine once the drop includes the items dimensions
// atm. the item is dropped in the surrounding slots dependent on the mouse offset to the center of the hovered slot.
// this offset needs to be calculated in relation to the center of the drag display

// rework item comparison
// compare rings and weapons to both slots
#endregion TODO

#region PLAYER STATS DETAILS
// hoverable playerStatDisplays => tooltip baseValue and mod sum per mod type

// DPS display
#endregion PLAYER STATS DETAILS

#region COMBAT SIMULATION
// Weapons should have attackSpeed multiply mod
// Weapons should set Damage base value on equip

// editable Dummy CharacterStats

// add skills to deal damage with
// HUD to show skills
#endregion COMBAT SIMULATION

#region CRAFTING SYSTEM
// adjustable affixe amount
// adjustable affixes
// adjustable affixe values
// adjustable item rarity
// lock affixes
// ...
#endregion CRAFTING SYSTEM

#region CRAFTABLE SKILLS
// base skills have a set of mods
// crafting allows to adjust, add or combine mods
/* sample mods are:
 * damageType
 * delayTime
 * radius
 * projectileAmount
 * ... */
#endregion CRAFTABLE SKILLS

#region ITEM SOCKETS
// implement the simplest version of socketing you can come up with
// design socketables - this goes into attribute design
#endregion ITEM SOCKETS

#region ICEBOX
// Improved MagicFind:
// in D2 => Effective MF = (MF * Factor) / (MF + Factor), where Factor=250 for unique items, 500 for set items and 600 for rare items..

// Add stash tabs
// -> this might require to make each stashTab its own inventory to interact with

// add a source to statModifiers to remove all modifiers of that source 
#endregion ICEBOX

#region ITEM MOVEMENT
// research if "Handle Item" should instead send the package to the tradeProvider to decide what to do with that item based on the open inventory displays
#endregion ITEM MOVEMENT

#region POLISH
//
#endregion POLISH